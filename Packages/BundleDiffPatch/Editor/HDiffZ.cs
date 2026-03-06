
using System;
using System.Runtime.InteropServices;

namespace Editor
{
    public enum HdiffzCompressType
    {
        None,
        Zlib,
        Zstd,
        Lzma2
    }
    
    public static unsafe class HDiffZ
    {
        private const string DllName = "hdiffz";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr hdiffz_diff(
            IntPtr old_data, UIntPtr old_size,
            IntPtr new_data, UIntPtr new_size,
            HdiffzCompressType compress_type,
            UIntPtr match_block_size,
            UIntPtr thread_num);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr hdiffz_result_data(IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern UIntPtr hdiffz_result_size(IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void hdiffz_result_free(IntPtr result);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int hdiffz_check(
            IntPtr old_data, UIntPtr old_size,
            IntPtr new_data, UIntPtr new_size,
            IntPtr diff_data, UIntPtr diff_size);

        /// <summary>
        /// Create a compressed block diff between old and new data.
        /// </summary>
        public static byte[] Diff(
            ReadOnlySpan<byte> oldData,
            ReadOnlySpan<byte> newData,
            HdiffzCompressType compressType = HdiffzCompressType.Zlib,
            int matchBlockSize = 0,
            int threadNum = 1)
        {
            unsafe
            {
                fixed (byte* oldPtr = oldData)
                fixed (byte* newPtr = newData)
                {
                    IntPtr resultHandle = hdiffz_diff(
                        (IntPtr)oldPtr, (UIntPtr)oldData.Length,
                        (IntPtr)newPtr, (UIntPtr)newData.Length,
                        compressType,
                        (UIntPtr)matchBlockSize,
                        (UIntPtr)threadNum);

                    if (resultHandle == IntPtr.Zero)
                        throw new InvalidOperationException("hdiffz_diff failed.");

                    try
                    {
                        var ptr = hdiffz_result_data(resultHandle);
                        var size = (int)hdiffz_result_size(resultHandle);

                        var result = new byte[size];
                        new ReadOnlySpan<byte>((void*)ptr, size).CopyTo(result);

                        return result;
                    }
                    finally
                    {
                        hdiffz_result_free(resultHandle);
                    }
                }
            }
        }

        /// <summary>
        /// Verify that a diff correctly transforms old data into new data.
        /// </summary>
        public static bool Check(
            ReadOnlySpan<byte> oldData,
            ReadOnlySpan<byte> newData,
            ReadOnlySpan<byte> diffData)
        {
            unsafe
            {
                fixed (byte* oldPtr = oldData)
                fixed (byte* newPtr = newData)
                fixed (byte* diffPtr = diffData)
                {
                    return hdiffz_check(
                        (IntPtr)oldPtr, (UIntPtr)oldData.Length,
                        (IntPtr)newPtr, (UIntPtr)newData.Length,
                        (IntPtr)diffPtr, (UIntPtr)diffData.Length) != 0;
                }
            }
        }
    }
}