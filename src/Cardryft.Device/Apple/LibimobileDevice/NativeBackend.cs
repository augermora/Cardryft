using Cardryft.Core;

namespace Cardryft.Device.Apple.LibimobileDevice;

// This is the tested managed boundary, not an implemented or validated C ABI.
internal enum NativeConnectionKind { Usb, Network, Unknown }
internal sealed record NativeDevice(string Identifier, NativeConnectionKind Connection);
internal enum DeviceMetadataField { DeviceName, ProductType, ProductVersion, BuildVersion }
internal enum NativeDeviceError { Disconnected, TransportUnavailable, NotTrusted, Restricted, Unsupported, InvalidResponse, Unknown }

internal sealed class NativeDeviceException(NativeDeviceError error) : Exception("Device operation unavailable.")
{
    public NativeDeviceError Error { get; } = error;
}

internal interface IDeviceMetadataConnection : IDisposable
{
    DeviceConnectionStatus Status { get; }
    DeviceTrustState Trust { get; }
    Task<string?> ReadAsync(DeviceMetadataField field, CancellationToken cancellationToken);
}

internal interface IDeviceNativeBackend
{
    DeviceDiagnostic CheckReadiness();
    Task<IReadOnlyList<NativeDevice>> GetUsbDevicesAsync(CancellationToken cancellationToken);
    Task<IDeviceMetadataConnection> ConnectUsbAsync(string identifier, CancellationToken cancellationToken);
}

internal sealed class UnvalidatedNativeBackend(NativeLibraryLoader loader) : IDeviceNativeBackend
{
    public DeviceDiagnostic CheckReadiness() => loader.CheckReadiness();
    public Task<IReadOnlyList<NativeDevice>> GetUsbDevicesAsync(CancellationToken cancellationToken)
        => throw new NativeDeviceException(NativeDeviceError.Unsupported);
    public Task<IDeviceMetadataConnection> ConnectUsbAsync(string identifier, CancellationToken cancellationToken)
        => throw new NativeDeviceException(NativeDeviceError.Unsupported);
}
