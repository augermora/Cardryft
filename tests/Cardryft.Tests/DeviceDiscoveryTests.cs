using System.Text.Json;
using Cardryft.Core;
using Cardryft.Device.Apple.LibimobileDevice;
using Cardryft.Storage;

namespace Cardryft.Tests;

public sealed class DeviceDiscoveryTests
{
    [Fact]
    public async Task NoDevices_IsNormalEmptyState()
    {
        var backend = new Backend();
        var result = await new LibimobileDeviceDiscovery(backend).DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DeviceConnectionStatus.NoDevice, result.Status);
        Assert.Empty(result.Devices);
        Assert.Empty(backend.Opened);
    }

    [Fact]
    public async Task TrustedDevice_ReadsOnlyFourApprovedFields_AndDisposesConnection()
    {
        var connection = new Connection();
        var backend = new Backend { Devices = [new("synthetic-private-id", NativeConnectionKind.Usb)], Open = _ => connection };
        var result = await new LibimobileDeviceDiscovery(backend).DiscoverAsync(TestContext.Current.CancellationToken);
        var device = Assert.Single(result.Devices);
        Assert.Equal(DeviceConnectionStatus.Connected, result.Status);
        Assert.Equal(DeviceTrustState.Trusted, device.Trust);
        Assert.Equal("Synthetic phone", device.Name);
        Assert.Equal("iPhoneTest,1", device.ProductType);
        Assert.Equal("27.0", device.ProductVersion);
        Assert.Equal("TESTBUILD", device.BuildVersion);
        Assert.Equal(Enum.GetValues<DeviceMetadataField>(), connection.Reads);
        Assert.Equal(1, connection.DisposeCount);
        Assert.DoesNotContain("synthetic-private-id", JsonSerializer.Serialize(result));
        Assert.DoesNotContain("Synthetic phone", device.ToString());
        Assert.DoesNotContain("synthetic-private-id", result.ToString());
    }

    [Fact]
    public async Task MultipleDevices_DeduplicateAndRejectNetworkCandidates()
    {
        var backend = new Backend { Devices = [new("one", NativeConnectionKind.Usb), new("one", NativeConnectionKind.Usb),
            new("wifi", NativeConnectionKind.Network), new("unknown", NativeConnectionKind.Unknown), new("two", NativeConnectionKind.Usb)] };
        var result = await new LibimobileDeviceDiscovery(backend).DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Devices.Count);
        Assert.Equal(new[] { "one", "two" }, backend.Opened);
    }

    [Fact]
    public async Task DisconnectAndReconnect_DoNotRetainPriorSnapshotOrIdentity()
    {
        var backend = new Backend { Devices = [new("old", NativeConnectionKind.Usb)] };
        var discovery = new LibimobileDeviceDiscovery(backend);
        Assert.Single((await discovery.DiscoverAsync(TestContext.Current.CancellationToken)).Devices);
        backend.Devices = [];
        Assert.Equal(DeviceConnectionStatus.NoDevice, (await discovery.DiscoverAsync(TestContext.Current.CancellationToken)).Status);
        backend.Devices = [new("new", NativeConnectionKind.Usb)];
        Assert.Single((await discovery.DiscoverAsync(TestContext.Current.CancellationToken)).Devices);
        Assert.Equal(new[] { "old", "new" }, backend.Opened);
    }

    [Theory]
    [InlineData(DeviceTrustState.NotTrusted, DeviceConnectionStatus.NotTrusted)]
    [InlineData(DeviceTrustState.Unknown, DeviceConnectionStatus.Connected)]
    [InlineData(DeviceTrustState.Trusted, DeviceConnectionStatus.Restricted)]
    public async Task UnconfirmedOrRestrictedAccess_DoesNotQueryMetadata(DeviceTrustState trust, DeviceConnectionStatus status)
    {
        var connection = new Connection { Trust = trust, Status = status };
        var backend = new Backend { Devices = [new("id", NativeConnectionKind.Usb)], Open = _ => connection };
        var device = Assert.Single((await new LibimobileDeviceDiscovery(backend).DiscoverAsync(TestContext.Current.CancellationToken)).Devices);
        Assert.Equal(status, device.Status);
        Assert.Equal(trust, device.Trust);
        Assert.Null(device.Name);
        Assert.Null(device.ProductVersion);
        Assert.Empty(connection.Reads);
        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public async Task MissingDependency_NeverEnumeratesOrConnects()
    {
        var backend = new Backend { Readiness = DeviceDiagnostic.MissingNativeBundle };
        var result = await new LibimobileDeviceDiscovery(backend).DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DeviceConnectionStatus.NativeDependencyUnavailable, result.Status);
        Assert.Equal(DeviceDiagnostic.MissingNativeBundle, result.Diagnostic);
        Assert.Equal(0, backend.EnumerationCount);
        Assert.Empty(backend.Opened);
    }

    [Theory]
    [InlineData((int)NativeDeviceError.Disconnected, DeviceConnectionStatus.NoDevice)]
    [InlineData((int)NativeDeviceError.TransportUnavailable, DeviceConnectionStatus.TransportUnavailable)]
    [InlineData((int)NativeDeviceError.NotTrusted, DeviceConnectionStatus.NotTrusted)]
    [InlineData((int)NativeDeviceError.Restricted, DeviceConnectionStatus.Restricted)]
    [InlineData((int)NativeDeviceError.Unsupported, DeviceConnectionStatus.Unsupported)]
    [InlineData((int)NativeDeviceError.InvalidResponse, DeviceConnectionStatus.UnknownFailure)]
    [InlineData((int)NativeDeviceError.Unknown, DeviceConnectionStatus.UnknownFailure)]
    public async Task BackendErrors_MapToGenericStates(int errorCode, DeviceConnectionStatus expected)
    {
        var error = (NativeDeviceError)errorCode;
        var backend = new Backend { EnumerateError = new NativeDeviceException(error) };
        var result = await new LibimobileDeviceDiscovery(backend).DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Status);
        Assert.Empty(result.Devices);
        Assert.DoesNotContain("id", new NativeDeviceException(error).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisconnectDuringOpen_RemovesDevice()
    {
        var backend = new Backend { Devices = [new("id", NativeConnectionKind.Usb)],
            Open = _ => throw new NativeDeviceException(NativeDeviceError.Disconnected) };
        var result = await new LibimobileDeviceDiscovery(backend).DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DeviceConnectionStatus.NoDevice, result.Status);
        Assert.Empty(result.Devices);
    }

    [Fact]
    public async Task ReadFailure_StillDisposesConnection()
    {
        var connection = new Connection { ReadError = new NativeDeviceException(NativeDeviceError.Restricted) };
        var backend = new Backend { Devices = [new("id", NativeConnectionKind.Usb)], Open = _ => connection };
        var device = Assert.Single((await new LibimobileDeviceDiscovery(backend).DiscoverAsync(TestContext.Current.CancellationToken)).Devices);
        Assert.Equal(DeviceConnectionStatus.Restricted, device.Status);
        Assert.Null(device.Name);
        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public async Task CancellationAfterOpen_DisposesConnectionBeforeReturning()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var connection = new Connection();
        var backend = new Backend { Devices = [new("id", NativeConnectionKind.Usb)],
            Open = _ => { cancellation.Cancel(); return connection; } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new LibimobileDeviceDiscovery(backend).DiscoverAsync(cancellation.Token));
        Assert.Equal(1, connection.DisposeCount);
        Assert.Empty(connection.Reads);
    }

    [Fact]
    public async Task PreCanceledRefresh_DoesNoBackendWork()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        var backend = new Backend();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new LibimobileDeviceDiscovery(backend).DiscoverAsync(cancellation.Token));
        Assert.Equal(0, backend.EnumerationCount);
    }

    [Fact]
    public async Task DeviceAndMetadataBounds_FailClosed()
    {
        var backend = new Backend { Devices = Enumerable.Range(0, 33).Select(i => new NativeDevice(i.ToString(), NativeConnectionKind.Usb)).ToArray() };
        var discovery = new LibimobileDeviceDiscovery(backend);
        Assert.Equal(DeviceDiagnostic.InvalidResponse, (await discovery.DiscoverAsync(TestContext.Current.CancellationToken)).Diagnostic);
        Assert.Empty(backend.Opened);
        var connection = new Connection { Name = new string('a', DeviceInfo.MaximumFieldLength + 1) };
        backend.Devices = [new("id", NativeConnectionKind.Usb)];
        backend.Open = _ => connection;
        var result = await discovery.DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DeviceConnectionStatus.UnknownFailure, Assert.Single(result.Devices).Status);
        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public async Task TextMapping_SanitizesControlsAndAllowsUnavailableFields()
    {
        var connection = new Connection { Name = "Test\r\nphone", MissingBuild = true };
        var backend = new Backend { Devices = [new("id", NativeConnectionKind.Usb)], Open = _ => connection };
        var device = Assert.Single((await new LibimobileDeviceDiscovery(backend).DiscoverAsync(TestContext.Current.CancellationToken)).Devices);
        Assert.Equal("Test  phone", device.Name);
        Assert.Null(device.BuildVersion);
    }

    [Fact]
    public async Task DeviceData_IsAbsentFromArtworkPersistence()
    {
        var backend = new Backend { Devices = [new("synthetic-private-id", NativeConnectionKind.Usb)] };
        var result = await new LibimobileDeviceDiscovery(backend).DiscoverAsync(TestContext.Current.CancellationToken);
        using var files = new ImageTestFiles();
        var project = files.PathFor("project.cardryft");
        new ProjectStore().Save(project, new ArtworkSession(files.CreateImage("source.png")));
        var json = File.ReadAllText(project);
        Assert.DoesNotContain("synthetic-private-id", json);
        Assert.DoesNotContain(Assert.Single(result.Devices).Name!, json);
        Assert.DoesNotContain("ProductVersion", json);
        Assert.DoesNotContain("Trust", json);
        Assert.DoesNotContain(typeof(DeviceInfo).GetProperties(), property => property.Name.Contains("Id", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class Backend : IDeviceNativeBackend
    {
        public DeviceDiagnostic Readiness { get; set; }
        public IReadOnlyList<NativeDevice> Devices { get; set; } = [];
        public Func<string, IDeviceMetadataConnection> Open { get; set; } = _ => new Connection();
        public Exception? EnumerateError { get; set; }
        public int EnumerationCount { get; private set; }
        public List<string> Opened { get; } = [];
        public DeviceDiagnostic CheckReadiness() => Readiness;
        public Task<IReadOnlyList<NativeDevice>> GetUsbDevicesAsync(CancellationToken cancellationToken)
        {
            EnumerationCount++;
            return EnumerateError is null ? Task.FromResult(Devices) : Task.FromException<IReadOnlyList<NativeDevice>>(EnumerateError);
        }
        public Task<IDeviceMetadataConnection> ConnectUsbAsync(string identifier, CancellationToken cancellationToken)
        {
            Opened.Add(identifier);
            return Task.FromResult(Open(identifier));
        }
    }

    private sealed class Connection : IDeviceMetadataConnection
    {
        public DeviceConnectionStatus Status { get; init; } = DeviceConnectionStatus.Connected;
        public DeviceTrustState Trust { get; init; } = DeviceTrustState.Trusted;
        public string Name { get; init; } = "Synthetic phone";
        public bool MissingBuild { get; init; }
        public Exception? ReadError { get; init; }
        public List<DeviceMetadataField> Reads { get; } = [];
        public int DisposeCount { get; private set; }
        public Task<string?> ReadAsync(DeviceMetadataField field, CancellationToken cancellationToken)
        {
            Reads.Add(field);
            if (ReadError is not null) return Task.FromException<string?>(ReadError);
            return Task.FromResult<string?>(field switch
            {
                DeviceMetadataField.DeviceName => Name,
                DeviceMetadataField.ProductType => "iPhoneTest,1",
                DeviceMetadataField.ProductVersion => "27.0",
                DeviceMetadataField.BuildVersion => MissingBuild ? null : "TESTBUILD",
                _ => throw new InvalidOperationException(),
            });
        }
        public void Dispose() => DisposeCount++;
    }
}
