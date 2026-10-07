namespace Cardryft.Core;

public interface IDeviceDiscovery
{
    Task<DeviceDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);
}

public enum DeviceConnectionStatus
{
    NoDevice, Connected, NotTrusted, Restricted, NativeDependencyUnavailable,
    TransportUnavailable, Unsupported, UnknownFailure,
}

public enum DeviceTrustState { Unavailable, Unknown, NotTrusted, Trusted }

public enum DeviceDiagnostic
{
    None, MissingNativeBundle, UnreviewedNativeBundle, UnsafeNativePath,
    UnsupportedArchitecture, UnsafeTransportOverride, NativeLocationUnavailable,
    TransportUnavailable, AccessRestricted, Unsupported, InvalidResponse, UnknownFailure,
}

public sealed record DeviceInfo
{
    public const int MaximumFieldLength = 256;
    public DeviceConnectionStatus Status { get; }
    public DeviceTrustState Trust { get; }
    public string? Name { get; }
    public string? ProductType { get; }
    public string? ProductVersion { get; }
    public string? BuildVersion { get; }

    public DeviceInfo(DeviceConnectionStatus status, DeviceTrustState trust, string? name = null,
        string? productType = null, string? productVersion = null, string? buildVersion = null)
    {
        if (!Enum.IsDefined(status) || !Enum.IsDefined(trust))
            throw new ArgumentException("Device state must be known to Cardryft.");
        Status = status;
        Trust = trust;
        Name = Validate(name);
        ProductType = Validate(productType);
        ProductVersion = Validate(productVersion);
        BuildVersion = Validate(buildVersion);
        if ((trust != DeviceTrustState.Trusted || status != DeviceConnectionStatus.Connected) &&
            (Name is not null || ProductType is not null || ProductVersion is not null || BuildVersion is not null))
            throw new ArgumentException("Metadata requires confirmed existing trust.");
    }

    private static string? Validate(string? value)
    {
        if (value is not null && (value.Length > MaximumFieldLength || value.Any(char.IsControl)))
            throw new ArgumentException("Device text must be bounded and contain no control characters.");
        return value;
    }

    public override string ToString() => $"USB device: {Status}, trust: {Trust}";
}

public sealed class DeviceDiscoveryResult
{
    public DeviceConnectionStatus Status { get; }
    public DeviceDiagnostic Diagnostic { get; }
    public IReadOnlyList<DeviceInfo> Devices { get; }

    public DeviceDiscoveryResult(DeviceConnectionStatus status, IEnumerable<DeviceInfo>? devices = null,
        DeviceDiagnostic diagnostic = DeviceDiagnostic.None)
    {
        Status = status;
        Diagnostic = diagnostic;
        Devices = Array.AsReadOnly((devices ?? []).ToArray());
    }

    public override string ToString() => $"{Devices.Count} USB device(s): {Status}; {Diagnostic}";
}
