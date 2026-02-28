using System;
using System.Runtime.InteropServices;

namespace BundleDiff.Editor;

public enum HdiffzCompressType
{
    None  = 0,
    Zlib  = 1,
    Zstd  = 2,
    Lzma2 = 3
}

internal sealed class HdiffzResultHandle : SafeHandle
{
    public HdiffzResultHandle() : base(IntPtr.Zero, true) { }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        HDiffZ.hdiffz_result_free(handle);
        return true;
    }
}

public static class HDiffZ
{
    private const string DllName = "hdiffz";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern HdiffzResultHandle hdiffz_diff(
        IntPtr old_data, UIntPtr old_size,
        IntPtr new_data, UIntPtr new_size,
        HdiffzCompressType compress_type,
        UIntPtr match_block_size,
        UIntPtr thread_num);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr hdiffz_result_data(IntPtr result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern UIntPtr hdiffz_result_size(IntPtr result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void hdiffz_result_free(IntPtr result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int hdiffz_check(
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
                using var handle = hdiffz_diff(
                    (IntPtr)oldPtr, (UIntPtr)oldData.Length,
                    (IntPtr)newPtr, (UIntPtr)newData.Length,
                    compressType,
                    (UIntPtr)matchBlockSize,
                    (UIntPtr)threadNum);

                if (handle.IsInvalid)
                    throw new InvalidOperationException("hdiffz_diff failed.");

                var ptr = hdiffz_result_data(handle.DangerousGetHandle());
                var size = (int)hdiffz_result_size(handle.DangerousGetHandle());

                var result = new byte[size];
                new ReadOnlySpan<byte>((void*)ptr, size).CopyTo(result);
                return result;
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