using System.Runtime.InteropServices;
using Cardryft.Core;
using Cardryft.Device.Apple.LibimobileDevice;

namespace Cardryft.Tests;

public sealed class NativeLibraryLoaderTests
{
    [Theory]
    [InlineData("relative")]
    [InlineData(@"C:relative")]
    [InlineData(@"\\server\share\native")]
    [InlineData(@"\\?\C:\native")]
    [InlineData(@"C:\native:stream")]
    public void UnsafeRoots_AreRejectedBeforeFileAccess(string root)
    {
        var inspector = new Paths();
        Assert.Equal(DeviceDiagnostic.UnsafeNativePath, new NativeLibraryLoader(root, Architecture.X64, null, inspector).CheckReadiness());
        Assert.Equal(0, inspector.ReadCount);
    }

    [Theory]
    [InlineData(Architecture.X86)]
    [InlineData(Architecture.Arm64)]
    public void NonX64_IsRejected(Architecture architecture)
        => Assert.Equal(DeviceDiagnostic.UnsupportedArchitecture, new NativeLibraryLoader(@"C:\app", architecture, null, new Paths()).CheckReadiness());

    [Theory]
    [InlineData("remote.example:1234")]
    [InlineData("127.0.0.1:1234")]
    [InlineData("")]
    public void AnyEndpointOverride_IsRejectedWithoutMutation(string endpoint)
    {
        var previous = Environment.GetEnvironmentVariable("USBMUXD_SOCKET_ADDRESS");
        Assert.Equal(DeviceDiagnostic.UnsafeTransportOverride, new NativeLibraryLoader(@"C:\app", Architecture.X64, endpoint, new Paths()).CheckReadiness());
        Assert.Equal(previous, Environment.GetEnvironmentVariable("USBMUXD_SOCKET_ADDRESS"));
    }

    [Fact]
    public void MappedNetworkDrive_AndReparseAncestors_AreRejected()
    {
        Assert.Equal(DeviceDiagnostic.UnsafeNativePath, new NativeLibraryLoader(@"C:\app", Architecture.X64, null, new Paths { Network = true }).CheckReadiness());
        Assert.Equal(DeviceDiagnostic.UnsafeNativePath, new NativeLibraryLoader(@"C:\app", Architecture.X64, null, new Paths
        { Attributes = path => path == @"C:\app" ? FileAttributes.ReparsePoint : FileAttributes.Directory }).CheckReadiness());
    }

    [Fact]
    public void MissingOrPresentDirectory_NeverEnablesUnreviewedCode()
    {
        Assert.Equal(DeviceDiagnostic.MissingNativeBundle, new NativeLibraryLoader(@"C:\app", Architecture.X64, null, new Paths()).CheckReadiness());
        Assert.Equal(DeviceDiagnostic.UnreviewedNativeBundle, new NativeLibraryLoader(@"C:\app", Architecture.X64, null, new Paths
        { Attributes = _ => FileAttributes.Directory }).CheckReadiness());
    }

    [Fact]
    public void UnavailablePath_RemainsNonfatal()
        => Assert.Equal(DeviceDiagnostic.NativeLocationUnavailable, new NativeLibraryLoader(@"C:\app", Architecture.X64, null, new Paths
        { Attributes = _ => throw new UnauthorizedAccessException("synthetic private path") }).CheckReadiness());

    [Fact]
    public async Task RealDefaultBackend_FailsClosedWithoutHardwareOrLibraries()
    {
        var result = await new LibimobileDeviceDiscovery().DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DeviceConnectionStatus.NativeDependencyUnavailable, result.Status);
        Assert.Empty(result.Devices);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnvalidatedBackend_CannotBypassPreflight(bool directoryPresent)
    {
        var inspector = new Paths
        {
            Attributes = _ => directoryPresent ? FileAttributes.Directory : null,
        };
        var backend = new UnvalidatedNativeBackend(new NativeLibraryLoader(@"C:\app", Architecture.X64, null, inspector));

        Assert.NotEqual(DeviceDiagnostic.None, backend.CheckReadiness());
        var enumeration = Assert.Throws<NativeDeviceException>(() => { _ = backend.GetUsbDevicesAsync(CancellationToken.None); });
        var connection = Assert.Throws<NativeDeviceException>(() => { _ = backend.ConnectUsbAsync("synthetic-id", CancellationToken.None); });
        Assert.Equal(NativeDeviceError.Unsupported, enumeration.Error);
        Assert.Equal(NativeDeviceError.Unsupported, connection.Error);
    }

    [Fact]
    public void ManagedMetadataContract_ExposesOnlyFourTypedReadsAndNoPairing()
    {
        Assert.Equal(["DeviceName", "ProductType", "ProductVersion", "BuildVersion"], Enum.GetNames<DeviceMetadataField>());
        var read = Assert.Single(typeof(IDeviceMetadataConnection).GetMethods(), method => method.Name == "ReadAsync");
        Assert.Equal([typeof(DeviceMetadataField), typeof(CancellationToken)], read.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(["CheckReadiness", "ConnectUsbAsync", "GetUsbDevicesAsync"],
            typeof(IDeviceNativeBackend).GetMethods().Select(method => method.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["ReadAsync", "get_Status", "get_Trust"],
            typeof(IDeviceMetadataConnection).GetMethods().Select(method => method.Name).Order(StringComparer.Ordinal));
    }

    private sealed class Paths : INativePathInspector
    {
        public bool Network { get; init; }
        public Func<string, FileAttributes?> Attributes { get; init; } = _ => null;
        public int ReadCount { get; private set; }
        public bool IsNetworkDrive(string root) => Network;
        public FileAttributes? GetAttributes(string path) { ReadCount++; return Attributes(path); }
    }
}
