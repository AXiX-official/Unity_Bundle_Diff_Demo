#nullable enable

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityAsset.NET.FileSystem;

namespace UnityAsset.NET.IO.Reader
{
    public struct BlockCacheKey : IEquatable<BlockCacheKey>
    {
        public readonly IVirtualFile File;
        public readonly int BlockIndex;
        
        public BlockCacheKey(IVirtualFile file, int blockIndex)
        {
            File = file;
            BlockIndex = blockIndex;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(File, BlockIndex);
        }
        
        public bool Equals(BlockCacheKey other)
        {
            return ReferenceEquals(File, other.File)
                   && BlockIndex == other.BlockIndex;
        }

        public override bool Equals(object? obj)
            => obj is BlockCacheKey other && Equals(other);
    }
    
    internal sealed class CacheEntry
    {
        public readonly Lazy<byte[]> Value;
        public readonly long Size;
        public LinkedListNode<BlockCacheKey>? Node;

        public CacheEntry(Func<byte[]> factory, long size)
        {
            Size = size;
            Value = new Lazy<byte[]>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }
    
    //TODO: 这个实现比Microsoft.Extensions.Caching.Memory差很多，但是集成Microsoft.Extensions.Caching.Memory的依赖dll太多
    public class BlockCache
    {
        private readonly ConcurrentDictionary<BlockCacheKey, CacheEntry> _dict = new();

        private readonly object _lruLock = new();
        private readonly LinkedList<BlockCacheKey> _lru = new();

        private long _maxSize;
        private long _currentSize;

        public BlockCache(long maxSize)
        {
            _maxSize = maxSize;
        }
        
        public byte[] GetOrCreate(BlockCacheKey key, Func<byte[]> factory, long size)
        {
            var entry = _dict.GetOrAdd(key, k =>
            {
                var newEntry = new CacheEntry(factory, size);

                lock (_lruLock)
                {
                    newEntry.Node = _lru.AddFirst(k);
                    _currentSize += size;
                    TrimIfNeeded();
                }

                return newEntry;
            });

            Touch(entry);

            try
            {
                return entry.Value.Value;
            }
            catch
            {
                Remove(key);
                throw;
            }
        }

        private void Touch(CacheEntry entry)
        {
            lock (_lruLock)
            {
                if (entry.Node == null)
                    return;

                _lru.Remove(entry.Node);
                _lru.AddFirst(entry.Node);
            }
        }

        private void TrimIfNeeded()
        {
            while (_currentSize > _maxSize && _lru.Count > 0)
            {
                var last = _lru.Last!;
                _lru.RemoveLast();

                if (_dict.TryRemove(last.Value, out var removed))
                {
                    _currentSize -= removed.Size;

                    if (removed.Value.IsValueCreated)
                    {
                        ArrayPool<byte>.Shared.Return(removed.Value.Value);
                    }
                }
            }
        }

        public void Remove(BlockCacheKey key)
        {
            if (_dict.TryRemove(key, out var entry))
            {
                lock (_lruLock)
                {
                    if (entry.Node != null)
                        _lru.Remove(entry.Node);

                    _currentSize -= entry.Size;
                }

                if (entry.Value.IsValueCreated)
                {
                    ArrayPool<byte>.Shared.Return(entry.Value.Value);
                }
            }
        }

        public void Reset(long maxSize)
        {
            foreach (var entry in _dict.Values)
            {
                if (entry.Value.IsValueCreated)
                {
                    ArrayPool<byte>.Shared.Return(entry.Value.Value);
                }
            }

            _dict.Clear();

            lock (_lruLock)
            {
                _lru.Clear();
                _currentSize = 0;
                _maxSize = maxSize;
            }
        }

        public long CurrentSize => _currentSize;
        public int Count => _dict.Count;
    }
}