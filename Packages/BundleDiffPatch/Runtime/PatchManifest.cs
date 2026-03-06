#nullable enable

using System;
using System.Collections.Generic;

namespace BundleDiffPatch.Runtime
{
    /// <summary>
    /// Patch 清单，描述整个热更新包的内容
    /// </summary>
    public class PatchManifest
    {
        public string Version { get; set; } = string.Empty;
        public string BaseVersion { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public List<FileOperation> Operations { get; set; } = new();
    }
    
    /// <summary>
    /// 单个内部文件的操作
    /// </summary>
    public class FileOperation
    {
        public OperationType Type { get; set; }

        /// <summary>
        /// Bundle 文件路径（磁盘上的相对路径）
        /// </summary>
        public string BundlePath { get; set; } = string.Empty;

        /// <summary>
        /// 内部文件路径（CAB-xxx 或 CAB-xxx.resS）
        /// </summary>
        public string InternalPath { get; set; } = string.Empty;

        public string? OldHash { get; set; }

        public string? NewHash { get; set; }

        /// <summary>
        /// Patch 文件路径（相对于 patch 包根目录）
        /// </summary>
        public string? PatchFile { get; set; }

        /// <summary>
        /// 完整数据文件路径（用于 Add 操作）
        /// </summary>
        public string? DataFile { get; set; }

        public long OldSize { get; set; }

        public long NewSize { get; set; }
    }
    
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
}