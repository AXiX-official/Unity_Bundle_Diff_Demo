using BundleDiff.Core.Models;
using BundleDiff.Core.Utils;
using UnityAsset.NET.Files;
using System.Text.Json;
using BundleDiff.Core;
using UnityAsset.NET.IO;
using SharpHDiffPatch.Core;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.BundleFiles;
using UnityAsset.NET.IO.Reader;

namespace BundleDiff.Runtime;

/// <summary>
/// Runtime 端：应用 patch
/// </summary>
public class PatchApplier
{
    private readonly string _baseDir;      // 当前版本目录
    private readonly string _patchDir;     // patch 包目录
    private readonly string _outputDir;    // 输出目录

    public PatchApplier(string baseDir, string patchDir, string outputDir)
    {
        _baseDir = baseDir;
        _patchDir = patchDir;
        _outputDir = outputDir;
    }

    public async Task ApplyAsync(string manifestPath)
    {
        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<PatchManifest>(manifestJson)
            ?? throw new InvalidOperationException("Failed to deserialize manifest");

        var manager = new AssetManager();
        await manager.LoadDirectoryAsync(_baseDir);

        var operationsByBundle = manifest.Operations.GroupBy(op => op.BundlePath);

        foreach (var group in operationsByBundle)
        {
            var bundlePath = group.Key;
            var operations = group.ToList();

            // 处理 bundle 级别操作
            var bundleOp = operations.FirstOrDefault(op => op.Type is OperationType.AddBundle or OperationType.DeleteBundle
                or OperationType.AddRaw or OperationType.ModifyRaw or OperationType.DeleteRaw);
            if (bundleOp != null)
            {
                await ApplyFileLevelOperationAsync(bundleOp);
                continue;
            }

            await ApplyBundleOperationsAsync(bundlePath, operations, manager);
        }
    }

    private async Task ApplyFileLevelOperationAsync(FileOperation fileOp)
    {
        var outputPath = Path.Combine(_outputDir, fileOp.BundlePath);

        switch (fileOp.Type)
        {
            case OperationType.AddBundle:
            case OperationType.AddRaw:
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                var srcPath = Path.Combine(_patchDir, fileOp.DataFile!);
                File.Copy(srcPath, outputPath, overwrite: true);
                Console.WriteLine($"{fileOp.Type}: {fileOp.BundlePath}");
                break;
            }
            case OperationType.ModifyRaw:
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                var oldPath = Path.Combine(_baseDir, fileOp.BundlePath);
                var oldData = await File.ReadAllBytesAsync(oldPath);
                var patchPath = Path.Combine(_patchDir, fileOp.PatchFile!);
                var newData = await ApplyPatch(oldData, patchPath);
                await File.WriteAllBytesAsync(outputPath, newData);
                Console.WriteLine($"{fileOp.Type}: {fileOp.BundlePath}");
                break;
            }
            case OperationType.DeleteBundle:
            case OperationType.DeleteRaw:
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                Console.WriteLine($"{fileOp.Type}: {fileOp.BundlePath}");
                break;
            }
        }
    }

    private async Task ApplyBundleOperationsAsync(string bundlePath, List<FileOperation> operations, AssetManager manager)
    {
        var bundleFile = FindBundleFile(manager, bundlePath);
        if (bundleFile == null)
        {
            Console.WriteLine($"Warning: Bundle not found: {bundlePath}");
            return;
        }

        bool modified = false;

        foreach (var fileOp in operations)
        {
            switch (fileOp.Type)
            {
                case OperationType.Add:
                    await ApplyAddAsync(bundleFile, fileOp);
                    modified = true;
                    break;

                case OperationType.Modify:
                    await ApplyModifyAsync(bundleFile, fileOp, manager);
                    modified = true;
                    break;

                case OperationType.Delete:
                    ApplyDelete(bundleFile, fileOp);
                    modified = true;
                    break;
            }
        }

        if (modified)
        {
            var outputPath = Path.Combine(_outputDir, bundlePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var tmpPath = Path.GetTempFileName();
            bundleFile.Serialize(tmpPath, CompressionType.Lz4HC, CompressionType.Lz4HC);
            // TODO: 这个api无法覆盖原始文件，必须先写到临时文件再覆盖，效率较低，后续可以考虑改成直接写到目标路径
            File.Move(tmpPath, outputPath, overwrite: true);
            Console.WriteLine($"Bundle rebuilt: {outputPath}");
        }
    }

    private async Task ApplyAddAsync(BundleFile bundleFile, FileOperation fileOp)
    {
        var dataPath = Path.Combine(_patchDir, fileOp.DataFile!);
        var newData = await File.ReadAllBytesAsync(dataPath);

        if (!HashHelper.VerifyHash(newData, fileOp.NewHash!))
        {
            throw new InvalidOperationException($"Hash mismatch for {fileOp.InternalPath}");
        }

        var index = bundleFile.Files.FindIndex(file => file.Info.Path == fileOp.InternalPath);
        if (index >= 0)
        {
            throw new InvalidOperationException($"File already exists in bundle: {fileOp.InternalPath}");
        }
        
        var newWrapper = new FileWrapper(new MemoryReaderProvider(newData), new FileEntry(0, 0, 0, fileOp.InternalPath));
        bundleFile.Files.Add(newWrapper);
        
        Console.WriteLine($"Add: {fileOp.InternalPath}");
    }

    private async Task ApplyModifyAsync(BundleFile bundleFile, FileOperation fileOp, AssetManager manager)
    {
        var oldFile = manager.LoadedFiles[fileOp.InternalPath];
        var oldData = ExtractData(oldFile);

        if (!HashHelper.VerifyHash(oldData, fileOp.OldHash!))
        {
            throw new InvalidOperationException($"Old file hash mismatch for {fileOp.InternalPath}");
        }

        var patchPath = Path.Combine(_patchDir, fileOp.PatchFile!);

        var newData = await ApplyPatch(oldData, patchPath);
        
        await using var test = new FileStream("test", FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        await test.WriteAsync(newData);

        if (!HashHelper.VerifyHash(newData, fileOp.NewHash!))
        {
            throw new InvalidOperationException($"New file hash mismatch for {fileOp.InternalPath}");
        }

        var index = bundleFile.Files.FindIndex(file => file.Info.Path == fileOp.InternalPath);
        if (index < 0)
        {
            throw new InvalidOperationException($"File not found in bundle: {fileOp.InternalPath}");
        }

        var wrapper = bundleFile.Files[index];
        
        bundleFile.Files[index] = new FileWrapper(new MemoryReaderProvider(newData), wrapper.Info);
        
        Console.WriteLine($"Modify: {fileOp.InternalPath}");
    }

    private void ApplyDelete(BundleFile bundleFile, FileOperation fileOp)
    {
        var index = bundleFile.Files.FindIndex(file => file.Info.Path == fileOp.InternalPath);
        if (index < 0)
        {
            throw new InvalidOperationException($"File not found in bundle: {fileOp.InternalPath}");
        }
        
        bundleFile.Files.RemoveAt(index);
        
        Console.WriteLine($"Delete: {fileOp.InternalPath}");
    }

    private BundleFile? FindBundleFile(AssetManager manager, string bundlePath)
    {
        var baseDirFull = Path.GetFullPath(_baseDir);
        foreach (var (virtualFile, file) in manager.VirtualFileToFileMap)
        {
            var relativePath = Path.GetRelativePath(baseDirFull, virtualFile.Path);
            if (relativePath == bundlePath && file is BundleFile bundleFile)
            {
                return bundleFile;
            }
        }
        return null;
    }

    private byte[] ExtractData(IFile file)
    {
        if (file is IReaderProvider provider)
        {
            var reader = provider.CreateReader();
            return reader.ReadBytes((int)reader.Length);
        }
        throw new InvalidOperationException($"File does not implement IReaderProvider");
    }

    private async Task<byte[]> ApplyPatch(byte[] oldData, string patchPath)
    {
        HDiffPatch patcher = new HDiffPatch();
        HDiffPatch.LogVerbosity = Verbosity.Verbose;
        patcher.Initialize(patchPath);
        // workaround: SharpHDiffPatch 只能处理文件路径，无法直接处理内存数据，所以只能写临时文件
        var tmpFileIn = Path.GetTempFileName();
        var tmpFileOut = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tmpFileIn, oldData);
        patcher.Patch(tmpFileIn, tmpFileOut, true, CancellationToken.None, false, true);
        var newData = await File.ReadAllBytesAsync(tmpFileOut);
        File.Delete(tmpFileIn);
        File.Delete(tmpFileOut);
        return newData;
    }
}
