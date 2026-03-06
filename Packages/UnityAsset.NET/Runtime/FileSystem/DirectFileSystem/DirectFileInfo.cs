using System.IO;
using System.IO.MemoryMappedFiles;

namespace UnityAsset.NET.FileSystem.DirectFileSystem
{
    public class DirectFileInfo : IVirtualFileInfo
    {
        public MemoryMappedFile Handle { get; }
        public string Path { get; }
        public string Name { get; }
        public long Length { get; }
        public FileType FileType { get; }
    
        public DirectFileInfo(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            Length = new FileInfo(path).Length;
            Handle = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            FileType = FileTypeHelper.GetFileType(this);
        }
    
        public IVirtualFile GetFile() => new DirectFile(Handle, 0, Length);
    }
}