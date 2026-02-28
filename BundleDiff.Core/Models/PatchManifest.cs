using System.Text.Json.Serialization;

namespace BundleDiff.Core.Models;

/// <summary>
/// Patch 清单，描述整个热更新包的内容
/// </summary>
public class PatchManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("baseVersion")]
    public string BaseVersion { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("operations")]
    public List<FileOperation> Operations { get; set; } = new();
}

/// <summary>
/// 单个内部文件的操作
/// </summary>
public class FileOperation
{
    [JsonPropertyName("type")]
    public OperationType Type { get; set; }

    /// <summary>
    /// Bundle 文件路径（磁盘上的相对路径）
    /// </summary>
    [JsonPropertyName("bundlePath")]
    public string BundlePath { get; set; } = string.Empty;

    /// <summary>
    /// 内部文件路径（CAB-xxx 或 CAB-xxx.resS）
    /// </summary>
    [JsonPropertyName("internalPath")]
    public string InternalPath { get; set; } = string.Empty;

    [JsonPropertyName("oldHash")]
    public string? OldHash { get; set; }

    [JsonPropertyName("newHash")]
    public string? NewHash { get; set; }

    /// <summary>
    /// Patch 文件路径（相对于 patch 包根目录）
    /// </summary>
    [JsonPropertyName("patchFile")]
    public string? PatchFile { get; set; }

    /// <summary>
    /// 完整数据文件路径（用于 Add 操作）
    /// </summary>
    [JsonPropertyName("dataFile")]
    public string? DataFile { get; set; }

    [JsonPropertyName("oldSize")]
    public long OldSize { get; set; }

    [JsonPropertyName("newSize")]
    public long NewSize { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationType
{
    Add,
    Modify,
    Delete,
    AddBundle,
    DeleteBundle,
    AddRaw,
    ModifyRaw,
    DeleteRaw
}
