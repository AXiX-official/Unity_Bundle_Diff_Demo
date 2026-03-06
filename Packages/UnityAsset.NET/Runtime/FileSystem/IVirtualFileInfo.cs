using System.IO.MemoryMappedFiles;

namespace UnityAsset.NET.FileSystem
{
    public interface IVirtualFileInfo
    {
        public MemoryMappedFile Handle { get; }
        public string Path { get; }
        public string Name { get; }
        public long Length { get; }
        public FileType FileType { get; }
        public IVirtualFile GetFile();
    }
}