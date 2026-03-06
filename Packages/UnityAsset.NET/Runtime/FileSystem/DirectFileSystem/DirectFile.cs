#nullable enable

using System;
using System.IO.MemoryMappedFiles;

namespace UnityAsset.NET.FileSystem.DirectFileSystem
{
    public class DirectFile : IVirtualFile, IEquatable<DirectFile>
    {
        public MemoryMappedFile Handle { get; }
        public long Length { get; }
        public long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }
        protected readonly long _start;
        private long _position;
        
        public DirectFile(MemoryMappedFile handle, long start, long length)
        {
            Handle = handle;
            _start = start;
            Length = length;
        }
        
        public virtual uint Read(byte[] buffer, uint offset, uint count)
        {
            var toRead = Math.Min(count, Length - _position);

            using var accessor = Handle.CreateViewAccessor(_start + _position, toRead, MemoryMappedFileAccess.Read);
            int read = accessor.ReadArray(0, buffer, 0, (int)count);

            _position += read;
            return (uint)read;
        }
        
        public virtual IVirtualFile Clone()
        {
            var ret = new DirectFile(Handle, _start, Length);
            ret.Position = Position;
            return ret;
        }
    
        public bool Equals(DirectFile? other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other is null)
                return false;

            return ReferenceEquals(Handle, other.Handle)
                   && _start == other._start
                   && Length == other.Length;
        }

        public override bool Equals(object? obj)
            => obj is DirectFile other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Handle,
                _start,
                Length);
        }
    }
}