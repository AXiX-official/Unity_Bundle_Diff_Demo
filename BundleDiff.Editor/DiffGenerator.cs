using BundleDiff.Core;
using BundleDiff.Core.Models;
using BundleDiff.Core.Utils;
using UnityAsset.NET.Files;
using UnityAsset.NET.IO;
using System.Text.Json;

namespace BundleDiff.Editor;

/// <summary>
/// Editor 端：生成 diff 和构建清单
/// </summary>
public class DiffGenerator
{
    private readonly string _oldDir;
    private readonly string _newDir;
    private readonly string _outputDir;

    public DiffGenerator(string oldDir, string newDir, string outputDir)
    {
        _oldDir = oldDir;
        _newDir = newDir;
        _outputDir = outputDir;
    }

    public async Task<PatchManifest> GenerateAsync(string version, string baseVersion)
    {
        Directory.CreateDirectory(Path.Combine(_outputDir, "patches"));

        var oldMgr = new AssetManager();
        await oldMgr.LoadDirectoryAsync(_oldDir);

        var newMgr = new AssetManager();
        await newMgr.LoadDirectoryAsync(_newDir);

        var oldBundleMap = BuildBundleMap(oldMgr, _oldDir);
        var newBundleMap = BuildBundleMap(newMgr, _newDir);

        var manifest = new PatchManifest
        {
            Version = version,
            BaseVersion = baseVersion,
            Timestamp = DateTime.UtcNow
        };

        var allBundlePaths = newBundleMap.Keys.Union(oldBundleMap.Keys).ToHashSet();

        foreach (var bundlePath in allBundlePaths)
        {
            var operations = CompareBundle(
                bundlePath,
                oldBundleMap.GetValueOrDefault(bundlePath),
                newBundleMap.GetValueOrDefault(bundlePath),
                oldMgr,
                newMgr
            );

            manifest.Operations.AddRange(operations);
        }
        
        manifest.Operations.AddRange(CompareRawFiles(oldMgr, newMgr));

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(_outputDir, "manifest.json"), manifestJson);

        return manifest;
    }

    /// <summary>
    /// 构建 bundlePath -> 内部文件列表 的映射
    /// </summary>
    private Dictionary<string, HashSet<string>> BuildBundleMap(AssetManager manager, string baseDir)
    {
        var map = new Dictionary<string, HashSet<string>>();
        var baseDirFull = Path.GetFullPath(baseDir);

        foreach (var (virtualFile, file) in manager.VirtualFileToFileMap)
        {
            if (file is BundleFile bundleFile)
            {
                var bundlePath = Path.GetRelativePath(baseDirFull, virtualFile.Path);
                var internalPaths = new HashSet<string>();

                foreach (var fw in bundleFile.Files)
                {
                    internalPaths.Add(fw.Info.Path);
                }

                map[bundlePath] = internalPaths;
            }
        }

        return map;
    }

    /// <summary>
    /// 对比单个 bundle 的变化
    /// </summary>
    private List<FileOperation> CompareBundle(
        string bundlePath,
        HashSet<string>? oldInternalPaths,
        HashSet<string>? newInternalPaths,
        AssetManager oldMgr,
        AssetManager newMgr)
    {
        var operations = new List<FileOperation>();

        // 整个 bundle 新增
        if (oldInternalPaths == null || oldInternalPaths.Count == 0)
        {
            var dataFile = $"patches/{SanitizePath(bundlePath)}.bundle";
            // 直接复制新版本的原始 bundle 文件
            var newBundlePath = Path.Combine(_newDir, bundlePath);
            File.Copy(newBundlePath, Path.Combine(_outputDir, dataFile), overwrite: true);

            operations.Add(new FileOperation
            {
                Type = OperationType.AddBundle,
                BundlePath = bundlePath,
                DataFile = dataFile
            });
            return operations;
        }

        // 整个 bundle 删除
        if (newInternalPaths == null || newInternalPaths.Count == 0)
        {
            operations.Add(new FileOperation
            {
                Type = OperationType.DeleteBundle,
                BundlePath = bundlePath
            });
            return operations;
        }

        // 内部文件级别的对比
        foreach (var internalPath in newInternalPaths.Except(oldInternalPaths))
        {
            var newFile = newMgr.LoadedFiles[internalPath];
            var newData = ExtractData(newFile);
            var newHash = HashHelper.ComputeHash(newData);

            var dataFile = $"patches/{SanitizePath(bundlePath)}_{SanitizePath(internalPath)}.full";
            File.WriteAllBytes(Path.Combine(_outputDir, dataFile), newData.ToArray());

            operations.Add(new FileOperation
            {
                Type = OperationType.Add,
                BundlePath = bundlePath,
                InternalPath = internalPath,
                NewHash = newHash,
                DataFile = dataFile,
                NewSize = newData.Length
            });
        }

        foreach (var internalPath in oldInternalPaths.Except(newInternalPaths))
        {
            var oldFile = oldMgr.LoadedFiles[internalPath];
            var oldData = ExtractData(oldFile);
            var oldHash = HashHelper.ComputeHash(oldData);

            operations.Add(new FileOperation
            {
                Type = OperationType.Delete,
                BundlePath = bundlePath,
                InternalPath = internalPath,
                OldHash = oldHash,
                OldSize = oldData.Length
            });
        }

        foreach (var internalPath in oldInternalPaths.Intersect(newInternalPaths))
        {
            var oldFile = oldMgr.LoadedFiles[internalPath];
            var newFile = newMgr.LoadedFiles[internalPath];

            var oldData = ExtractData(oldFile);
            var newData = ExtractData(newFile);

            var oldHash = HashHelper.ComputeHash(oldData);
            var newHash = HashHelper.ComputeHash(newData);

            if (oldHash != newHash)
            {
                var diffData = HDiffZ.Diff(oldData, newData, HdiffzCompressType.Zstd);
                var patchFile = $"patches/{SanitizePath(bundlePath)}_{SanitizePath(internalPath)}.diff";
                File.WriteAllBytes(Path.Combine(_outputDir, patchFile), diffData);

                operations.Add(new FileOperation
                {
                    Type = OperationType.Modify,
                    BundlePath = bundlePath,
                    InternalPath = internalPath,
                    OldHash = oldHash,
                    NewHash = newHash,
                    PatchFile = patchFile,
                    OldSize = oldData.Length,
                    NewSize = newData.Length
                });
            }
        }

        return operations;
    }

    /// <summary>
    /// 提取未压缩数据
    /// </summary>
    private ReadOnlySpan<byte> ExtractData(IFile file)
    {
        if (file is IReaderProvider provider)
        {
            var reader = provider.CreateReader();
            return reader.ReadBytes((int)reader.Length);
        }
        throw new InvalidOperationException($"File does not implement IReaderProvider");
    }

    private string SanitizePath(string path)
    {
        return path.Replace('/', '_').Replace('\\', '_').Replace('.', '_');
    }

    private HashSet<string> GetRawRelativePaths(AssetManager manager, string baseDir)
    {
        var baseDirFull = Path.GetFullPath(baseDir);
        var paths = new HashSet<string>();
        foreach (var file in manager.RawFiles)
        {
            paths.Add(Path.GetRelativePath(baseDirFull, file.Path));
        }
        return paths;
    }

    private List<FileOperation> CompareRawFiles(AssetManager oldMgr, AssetManager newMgr)
    {
        var operations = new List<FileOperation>();

        var oldRawPaths = GetRawRelativePaths(oldMgr, _oldDir);
        var newRawPaths = GetRawRelativePaths(newMgr, _newDir);
        
        foreach (var relPath in newRawPaths.Except(oldRawPaths))
        {
            var dataFile = $"patches/{SanitizePath(relPath)}.raw";
            var srcPath = Path.Combine(_newDir, relPath);
            File.Copy(srcPath, Path.Combine(_outputDir, dataFile), overwrite: true);

            operations.Add(new FileOperation
            {
                Type = OperationType.AddRaw,
                BundlePath = relPath,
                DataFile = dataFile
            });
        }
        
        foreach (var relPath in oldRawPaths.Except(newRawPaths))
        {
            operations.Add(new FileOperation
            {
                Type = OperationType.DeleteRaw,
                BundlePath = relPath
            });
        }
        
        foreach (var relPath in oldRawPaths.Intersect(newRawPaths))
        {
            var oldBytes = File.ReadAllBytes(Path.Combine(_oldDir, relPath));
            var newBytes = File.ReadAllBytes(Path.Combine(_newDir, relPath));

            var oldHash = HashHelper.ComputeHash(oldBytes);
            var newHash = HashHelper.ComputeHash(newBytes);

            if (oldHash != newHash)
            {
                var diffData = HDiffZ.Diff(oldBytes, newBytes, HdiffzCompressType.Zstd);
                var patchFile = $"patches/{SanitizePath(relPath)}.raw.diff";
                File.WriteAllBytes(Path.Combine(_outputDir, patchFile), diffData);

                operations.Add(new FileOperation
                {
                    Type = OperationType.ModifyRaw,
                    BundlePath = relPath,
                    OldHash = oldHash,
                    NewHash = newHash,
                    PatchFile = patchFile,
                    OldSize = oldBytes.Length,
                    NewSize = newBytes.Length
                });
            }
        }

        return operations;
    }
}
