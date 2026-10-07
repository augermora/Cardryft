using System.Runtime.InteropServices;
using Cardryft.Core;

namespace Cardryft.Device.Apple.LibimobileDevice;

internal interface INativePathInspector
{
    bool IsNetworkDrive(string root);
    FileAttributes? GetAttributes(string path);
}

internal sealed class NativePathInspector : INativePathInspector
{
    public bool IsNetworkDrive(string root) => new DriveInfo(root).DriveType == DriveType.Network;
    public FileAttributes? GetAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
}

// Preflight only. No LoadLibrary/NativeLibrary.Load, DllImport, PATH search or device calls.
// The gate deliberately cannot become ready by dropping unreviewed DLLs into a directory.
internal sealed class NativeLibraryLoader(string applicationRoot, Architecture architecture,
    string? usbmuxEndpointOverride, INativePathInspector? pathInspector = null)
{
    private readonly INativePathInspector paths = pathInspector ?? new NativePathInspector();

    public DeviceDiagnostic CheckReadiness()
    {
        if (architecture != Architecture.X64) return DeviceDiagnostic.UnsupportedArchitecture;
        if (usbmuxEndpointOverride is not null) return DeviceDiagnostic.UnsafeTransportOverride;
        if (!Path.IsPathFullyQualified(applicationRoot) || applicationRoot.StartsWith(@"\\", StringComparison.Ordinal) ||
            applicationRoot.StartsWith("//", StringComparison.Ordinal) || applicationRoot.AsSpan(2).Contains(':'))
            return DeviceDiagnostic.UnsafeNativePath;
        try
        {
            var root = Path.GetFullPath(applicationRoot);
            if (paths.IsNetworkDrive(Path.GetPathRoot(root)!)) return DeviceDiagnostic.UnsafeNativePath;
            var directory = Path.Combine(root, "native", "win-x64");
            for (var current = directory; current is not null; current = Path.GetDirectoryName(current))
            {
                if (paths.GetAttributes(current) is { } attributes && attributes.HasFlag(FileAttributes.ReparsePoint))
                    return DeviceDiagnostic.UnsafeNativePath;
            }
            return paths.GetAttributes(directory) is null
                ? DeviceDiagnostic.MissingNativeBundle : DeviceDiagnostic.UnreviewedNativeBundle;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            return DeviceDiagnostic.NativeLocationUnavailable;
        }
    }
}
