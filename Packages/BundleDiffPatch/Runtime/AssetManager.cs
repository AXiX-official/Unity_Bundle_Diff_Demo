#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityAsset.NET;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.FileSystem.DirectFileSystem;
using UnityAsset.NET.IO.Reader;

namespace BundleDiffPatch.Runtime
{
    public class AssetManager
    {
        private IFileSystem _fileSystem;

        public Dictionary<string, IFile> LoadedFiles = new ();

        public ConcurrentDictionary<IVirtualFileInfo, IFile> VirtualFileToFileMap = new();
    
        public ConcurrentBag<IVirtualFileInfo> RawFiles = new();
    
        public AssetManager(IFileSystem? fileSystem = null, IFileSystem.ErrorHandler? onError = null)
        {
            _fileSystem = fileSystem ?? new DirectFileSystem(onError);
            _fileSystem.RecordUnknownFiles = true;
        }
        
        public void SetFileSystem(IFileSystem fileSystem)
        {
            Clear();
            _fileSystem = fileSystem;
            _fileSystem.RecordUnknownFiles = true;
        }
    
        public async Task LoadAsync(List<string> paths, bool ignoreDuplicatedFiles = false, IProgress<LoadProgress>? progress = null)
        {
            var virtualFiles = await _fileSystem.LoadAsync(paths, progress);
        
            await LoadAsync(virtualFiles, ignoreDuplicatedFiles, progress);
        }

        public async Task LoadDirectoryAsync(string directoryPath, bool ignoreDuplicatedFiles = false, IProgress<LoadProgress>? progress = null)
        {
            string[] filePaths = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            await LoadAsync(filePaths.ToList(), ignoreDuplicatedFiles, progress);
        }
        
        public async Task LoadAsync(List<IVirtualFileInfo> files, bool ignoreDuplicatedFiles = false, IProgress<LoadProgress>? progress = null)
        {
            await Task.Run(() =>
            {
                var fileWrappers = new ConcurrentBag<(string, IFile)>();
                int progressCount = 0;
                var total = files.Count;
                
                Parallel.ForEach(files, file =>
                {
                    switch (file.FileType)
                    {
                        case FileType.BundleFile:
                        {
                            var bundleFile = new BundleFile(file);
                            foreach (var fw in bundleFile.Files)
                            {
                                fileWrappers.Add((fw.Info.Path, fw.File));
                            }

                            VirtualFileToFileMap[file] = bundleFile;
                            break;
                        }
                        case FileType.SerializedFile:
                        {
                            // 暂时按纯数据文件处理，因为无压缩
                            RawFiles.Add(file);
                            break;
                        }
                        default:
                        {
                            RawFiles.Add(file);
                            break;
                        }
                    }
                    int currentProgress = Interlocked.Increment(ref progressCount);
                    progress?.Report(new LoadProgress($"AssetManager: Loading {file.Name}", total, currentProgress));
                });
                
                RemoveSingleReferenceBlocks();
                BlockReader.Cache = new(maxSize: TotalBlockSize * 3 / 4); // it works good for BuildSceneHierarchy
                BlockReader.AssetToBlockCache = AssetToBlockCache;
                
                LoadedFiles = new Dictionary<string, IFile>();
                
                foreach (var (path, file) in fileWrappers)
                {
                    if (!LoadedFiles.TryAdd(path, file) && !ignoreDuplicatedFiles)
                    {
                        throw new InvalidOperationException($"File {path} already loaded");
                    }
                }
            });
        }
        
        public static ConcurrentDictionary<BlockCacheKey, (int parsed, int total, long size)> AssetToBlockCache = new();
    
        public static long TotalBlockSize => AssetToBlockCache.Values.Select(v => v.size).Sum();
        
        public static void RemoveSingleReferenceBlocks()
        {
            var keysToRemove = AssetToBlockCache
                .Where(kvp => kvp.Value.total <= 2)
                .Select(kvp => kvp.Key);
            foreach (var key in keysToRemove)
                AssetToBlockCache.TryRemove(key, out _);
        }
        
        public void Clear()
        {
            VirtualFileToFileMap = new();
            RawFiles = new();
            LoadedFiles = new();
        
            _fileSystem.Clear();
        
            BlockReader.Cache.Reset(Setting.DefaultBlockCacheSize);
            BlockReader.AssetToBlockCache = new();
        }
    }
}