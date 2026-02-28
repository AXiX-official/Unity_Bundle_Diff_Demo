using System.Collections.Concurrent;
using System.Collections.Frozen;
using UnityAsset.NET;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.FileSystem;
using UnityAsset.NET.IO.Reader;

namespace BundleDiff.Core;

public class AssetManager
{
    private IFileSystem _fileSystem;

    public FrozenDictionary<string, IFile> LoadedFiles = new Dictionary<string, IFile>().ToFrozenDictionary();

    public ConcurrentDictionary<IVirtualFileInfo, IFile> VirtualFileToFileMap = new();
    
    public ConcurrentBag<IVirtualFileInfo> RawFiles = new();
    
    public UnityRevision? Version { get; private set; }
    public BuildTarget? BuildTarget { get; private set; }
    
    public AssetManager(IFileSystem? fileSystem = null, IFileSystem.ErrorHandler? onError = null)
    {
        _fileSystem = fileSystem ?? new MyFileSystem(onError);
    }
    
    public void SetFileSystem(IFileSystem fileSystem)
    {
        Clear();
        _fileSystem = fileSystem;
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
            
            bool anyTypeTreeDisabled = false;

            Parallel.ForEach(files, file =>
            {
                switch (file.FileType)
                {
                    case FileType.BundleFile:
                    {
                        var bundleFile = new BundleFile(file, lazyLoad: true);
                        foreach (var fw in bundleFile.Files)
                        {
                            fileWrappers.Add((fw.Info.Path, fw.File));
                        }

                        VirtualFileToFileMap[file] = bundleFile;
                        break;
                    }
                    case FileType.SerializedFile:
                    {
                        var serializedFile = new SerializedFile(file);
                        fileWrappers.Add((file.Name, serializedFile));

                        VirtualFileToFileMap[file] = serializedFile;
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
            
            BlockReader.RemoveSingleReferenceBlocks();
            BlockReader.Cache = new(maxSize: BlockReader.TotalBlockSize * 3 / 4); // it works good for BuildSceneHierarchy
            
            var tmpLoadedFilesDict = new Dictionary<string, IFile>();
            
            foreach (var (path, file) in fileWrappers)
            {
                if (!tmpLoadedFilesDict.TryAdd(path, file) && !ignoreDuplicatedFiles)
                {
                    throw new InvalidOperationException($"File {path} already loaded");
                }
            }

            LoadedFiles = tmpLoadedFilesDict.ToFrozenDictionary();

            var firstFile = LoadedFiles.Values
                .FirstOrDefault(file => file is SerializedFile);
            if (firstFile is SerializedFile firstSerializedFile)
            {
                BuildTarget = firstSerializedFile.Metadata.TargetPlatform;
                Version = firstSerializedFile.Metadata.UnityVersion;
            }
        });
    }
    
    public void Clear()
    {
        VirtualFileToFileMap = new();
        RawFiles = new();
        LoadedFiles = new Dictionary<string, IFile>().ToFrozenDictionary();
        Version = null;
        BuildTarget = null;
        
        _fileSystem.Clear();
        
        BlockReader.Cache.Reset(Setting.DefaultBlockCacheSize);
        BlockReader.AssetToBlockCache = new();
    }
}