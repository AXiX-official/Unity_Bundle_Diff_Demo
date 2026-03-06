using System;
using System.IO.MemoryMappedFiles;

namespace UnityAsset.NET.FileSystem
{
    public interface IVirtualFile
    {
        public MemoryMappedFile Handle { get; }
        public long Length { get; }
        public long Position { get; set; }
    
        public uint Read(byte[] buffer, uint offset, uint count);

        public byte[] ReadBytes(uint count)
        {
            var buffer = new byte[count];
            Read(buffer, 0, count);
            return buffer;
        }

        public void ReadExactly(byte[] buffer)
        {
            var byteRead = Read(buffer, 0, (uint)buffer.Length);
            if (byteRead != buffer.Length)
                throw new Exception($"No enough bytes to read. Expect {buffer.Length} bytes but got {byteRead} bytes.");
        }
        public void ReadExactly(byte[] buffer, uint offset, uint count)
        {
            var byteRead = Read(buffer, offset, count);
            if (byteRead != count)
                throw new Exception($"No enough bytes to read. Expect {count} bytes but got {byteRead} bytes.");
        }
        public IVirtualFile Clone();
    }
}