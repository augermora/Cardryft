using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Cardryft.Device.Apple.LibimobileDevice;

internal static class NativeDirectoryGuard
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicInfo
    {
        internal long CreationTime, LastAccessTime, LastWriteTime, ChangeTime;
        internal uint Attributes;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, out BasicInfo info, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint size, uint flags);

    internal static SafeFileHandle Open(string directory)
    {
        // Hold ancestors without delete sharing, opening the reparse point itself.
        var handle = CreateFileW(directory, 0x80, 3, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
        try
        {
            var actual = new StringBuilder(32768);
            var length = GetFinalPathNameByHandleW(handle, actual, (uint)actual.Capacity, 0);
            if (handle.IsInvalid || !GetFileInformationByHandleEx(handle, 0, out var info, (uint)Marshal.SizeOf<BasicInfo>()) ||
                (info.Attributes & (uint)FileAttributes.ReparsePoint) != 0 ||
                (info.Attributes & (uint)FileAttributes.Directory) == 0 || length == 0 || length >= actual.Capacity ||
                !string.Equals(actual.ToString().TrimEnd('\\'), (@"\\?\" + directory).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw new NativeRuntimeRejectedException();
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    internal static void VerifyFile(SafeFileHandle handle, string path)
    {
        var actual = new StringBuilder(32768);
        var length = GetFinalPathNameByHandleW(handle, actual, (uint)actual.Capacity, 0);
        if (handle.IsInvalid || !GetFileInformationByHandleEx(handle, 0, out var info, (uint)Marshal.SizeOf<BasicInfo>()) ||
            (info.Attributes & (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 ||
            length == 0 || length >= actual.Capacity ||
            !string.Equals(actual.ToString(), @"\\?\" + path, StringComparison.OrdinalIgnoreCase))
            throw new NativeRuntimeRejectedException();
    }
}
