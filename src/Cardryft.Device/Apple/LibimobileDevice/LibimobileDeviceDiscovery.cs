using System.Runtime.InteropServices;
using Cardryft.Core;

namespace Cardryft.Device.Apple.LibimobileDevice;

public sealed class LibimobileDeviceDiscovery : IDeviceDiscovery
{
    private const int MaximumDevices = 32;
    private readonly IDeviceNativeBackend backend;
    private readonly SemaphoreSlim gate = new(1, 1);

    public LibimobileDeviceDiscovery() : this(new UnvalidatedNativeBackend(new NativeLibraryLoader(
        AppContext.BaseDirectory, RuntimeInformation.ProcessArchitecture,
        Environment.GetEnvironmentVariable("USBMUXD_SOCKET_ADDRESS")))) { }

    internal LibimobileDeviceDiscovery(IDeviceNativeBackend backend)
        => this.backend = backend ?? throw new ArgumentNullException(nameof(backend));

    public async Task<DeviceDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var diagnostic = backend.CheckReadiness();
            if (diagnostic != DeviceDiagnostic.None)
                return new(DeviceConnectionStatus.NativeDependencyUnavailable, diagnostic: diagnostic);
            var candidates = await backend.GetUsbDevicesAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (candidates.Count > MaximumDevices)
                return new(DeviceConnectionStatus.UnknownFailure, diagnostic: DeviceDiagnostic.InvalidResponse);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var devices = new List<DeviceInfo>();
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidate.Connection != NativeConnectionKind.Usb) continue;
                if (string.IsNullOrWhiteSpace(candidate.Identifier) || candidate.Identifier.Length > 128)
                    return new(DeviceConnectionStatus.UnknownFailure, diagnostic: DeviceDiagnostic.InvalidResponse);
                if (!seen.Add(candidate.Identifier)) continue;
                var device = await ReadDeviceAsync(candidate.Identifier, cancellationToken).ConfigureAwait(false);
                if (device.Status != DeviceConnectionStatus.NoDevice) devices.Add(device);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new(devices.Count == 0 ? DeviceConnectionStatus.NoDevice : DeviceConnectionStatus.Connected, devices);
        }
        catch (OperationCanceledException) { throw; }
        catch (NativeDeviceException exception) { return FromError(exception.Error); }
        catch (Exception) { return new(DeviceConnectionStatus.UnknownFailure, diagnostic: DeviceDiagnostic.UnknownFailure); }
        finally { gate.Release(); }
    }

    private async Task<DeviceInfo> ReadDeviceAsync(string identifier, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await backend.ConnectUsbAsync(identifier, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (connection.Status != DeviceConnectionStatus.Connected || connection.Trust != DeviceTrustState.Trusted)
                return new(connection.Trust == DeviceTrustState.NotTrusted ? DeviceConnectionStatus.NotTrusted : connection.Status,
                    connection.Trust);
            // Four typed keys only; this boundary offers no Pair, StartService, generic dictionary or write API.
            var name = await ReadFieldAsync(connection, DeviceMetadataField.DeviceName, cancellationToken).ConfigureAwait(false);
            var product = await ReadFieldAsync(connection, DeviceMetadataField.ProductType, cancellationToken).ConfigureAwait(false);
            var version = await ReadFieldAsync(connection, DeviceMetadataField.ProductVersion, cancellationToken).ConfigureAwait(false);
            var build = await ReadFieldAsync(connection, DeviceMetadataField.BuildVersion, cancellationToken).ConfigureAwait(false);
            return new(DeviceConnectionStatus.Connected, DeviceTrustState.Trusted, name, product, version, build);
        }
        catch (NativeDeviceException exception)
        {
            var result = FromError(exception.Error);
            return new(result.Status, exception.Error == NativeDeviceError.NotTrusted ? DeviceTrustState.NotTrusted : DeviceTrustState.Unknown);
        }
    }

    private static async Task<string?> ReadFieldAsync(IDeviceMetadataConnection connection,
        DeviceMetadataField field, CancellationToken cancellationToken)
    {
        var value = await connection.ReadAsync(field, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return null;
        if (value.Length > DeviceInfo.MaximumFieldLength)
            throw new NativeDeviceException(NativeDeviceError.InvalidResponse);
        return string.Concat(value.EnumerateRunes().Select(rune => System.Text.Rune.IsControl(rune) ? " " : rune.ToString())).Trim();
    }

    private static DeviceDiscoveryResult FromError(NativeDeviceError error) => error switch
    {
        NativeDeviceError.Disconnected => new(DeviceConnectionStatus.NoDevice),
        NativeDeviceError.TransportUnavailable => new(DeviceConnectionStatus.TransportUnavailable, diagnostic: DeviceDiagnostic.TransportUnavailable),
        NativeDeviceError.NotTrusted => new(DeviceConnectionStatus.NotTrusted),
        NativeDeviceError.Restricted => new(DeviceConnectionStatus.Restricted, diagnostic: DeviceDiagnostic.AccessRestricted),
        NativeDeviceError.Unsupported => new(DeviceConnectionStatus.Unsupported, diagnostic: DeviceDiagnostic.Unsupported),
        NativeDeviceError.InvalidResponse => new(DeviceConnectionStatus.UnknownFailure, diagnostic: DeviceDiagnostic.InvalidResponse),
        NativeDeviceError.Timeout => new(DeviceConnectionStatus.TransportUnavailable, diagnostic: DeviceDiagnostic.TransportUnavailable),
        _ => new(DeviceConnectionStatus.UnknownFailure, diagnostic: DeviceDiagnostic.UnknownFailure),
    };
}
