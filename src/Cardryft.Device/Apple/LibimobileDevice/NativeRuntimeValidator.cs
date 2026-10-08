using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Cardryft.Device.Apple.LibimobileDevice;

internal sealed record NativeFilePin(string Name, long Size, string Sha256, IReadOnlyList<string> Imports);

internal sealed class NativeRuntimeRejectedException() : Exception("Native runtime validation failed.");

// Pins come from reviewed source, never from a JSON file beside the DLLs.
internal sealed class NativeRuntimeValidator(IReadOnlyList<NativeFilePin> pins,
    INativePathInspector? pathInspector = null)
{
    internal const long MaximumFileBytes = 16 * 1024 * 1024;
    private readonly INativePathInspector paths = pathInspector ?? new NativePathInspector();

    internal ValidatedNativeBundle Validate(string applicationRoot, Architecture architecture, string? endpointOverride)
    {
        var heldFiles = new List<IDisposable>();
        try
        {
            if (!OperatingSystem.IsWindows() || architecture != Architecture.X64 || endpointOverride is not null ||
                !Path.IsPathFullyQualified(applicationRoot) || applicationRoot.StartsWith(@"\\", StringComparison.Ordinal) ||
                applicationRoot.StartsWith("//", StringComparison.Ordinal) || applicationRoot.AsSpan(2).Contains(':'))
                throw new NativeRuntimeRejectedException();
            var root = Path.GetFullPath(applicationRoot);
            if (!string.Equals(root.TrimEnd('\\'), applicationRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
                paths.IsNetworkDrive(Path.GetPathRoot(root)!)) throw new NativeRuntimeRejectedException();
            var directory = Path.Combine(root, "native", "win-x64");
            var ancestorCount = 0;
            for (var current = directory; current is not null; current = Path.GetDirectoryName(current))
            {
                if (++ancestorCount > 256) throw new NativeRuntimeRejectedException();
                if (paths.GetAttributes(current) is { } attributes && attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new NativeRuntimeRejectedException();
                heldFiles.Add(NativeDirectoryGuard.Open(current));
            }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (pins.Count != 4 || pins.Any(pin => !names.Add(pin.Name) || Path.GetFileName(pin.Name) != pin.Name ||
                pin.Size is <= 0 or > MaximumFileBytes || pin.Sha256.Length != 64 || !pin.Sha256.All(Uri.IsHexDigit)))
                throw new NativeRuntimeRejectedException();
            var entries = Directory.EnumerateFileSystemEntries(directory).Take(pins.Count + 1).ToArray();
            if (entries.Length != pins.Count || entries.Any(entry => !names.Contains(Path.GetFileName(entry))))
                throw new NativeRuntimeRejectedException();
            foreach (var pin in pins)
            {
                var path = Path.Combine(directory, pin.Name);
                if (paths.GetAttributes(path) is not { } attributes ||
                    (attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                    throw new NativeRuntimeRejectedException();
                // Deny write/delete until every module is loaded. Validation never loads code.
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                heldFiles.Add(stream);
                NativeDirectoryGuard.VerifyFile(stream.SafeFileHandle, path);
                if (stream.Length != pin.Size || !string.Equals(Convert.ToHexString(SHA256.HashData(stream)),
                    pin.Sha256, StringComparison.OrdinalIgnoreCase)) throw new NativeRuntimeRejectedException();
                stream.Position = 0;
                var image = NativePeImage.Read(stream);
                if (!image.Imports.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(
                    pin.Imports.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
                    throw new NativeRuntimeRejectedException();
                if (pin.Name == "cardryft-device.dll" && !image.Exports.Order(StringComparer.Ordinal).SequenceEqual(
                    NativeShimAbi.ExportNames.Order(StringComparer.Ordinal))) throw new NativeRuntimeRejectedException();
            }
            return new ValidatedNativeBundle(directory, pins.Select(pin => pin.Name).ToArray(), heldFiles);
        }
        catch
        {
            foreach (var stream in heldFiles) stream.Dispose();
            throw new NativeRuntimeRejectedException();
        }
    }
}

internal sealed class ValidatedNativeBundle(string directory, IReadOnlyList<string> files,
    IReadOnlyList<IDisposable> heldFiles) : IDisposable
{
    internal string Directory { get; } = directory;
    internal IReadOnlyList<string> Files { get; } = files;
    public void Dispose() { foreach (var stream in heldFiles) stream.Dispose(); }
}
