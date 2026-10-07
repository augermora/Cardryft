using Cardryft.App;
using Cardryft.Core;

namespace Cardryft.Tests;

public sealed class DeviceRefreshTests
{
    [Fact]
    public async Task Refresh_IsSerialCoalescedAndSuppressesStaleResults()
    {
        var first = new TaskCompletionSource<DeviceDiscoveryResult>();
        var second = new TaskCompletionSource<DeviceDiscoveryResult>();
        var calls = 0;
        CancellationToken firstToken = default;
        var refresh = new DeviceRefreshController(new Discovery(token =>
        {
            calls++;
            if (calls == 1) { firstToken = token; return first.Task; }
            return second.Task;
        }));
        var published = 0;
        refresh.Changed += () => published++;
        refresh.Refresh();
        refresh.Refresh();
        refresh.Refresh();
        Assert.Equal(1, calls);
        Assert.True(firstToken.IsCancellationRequested);
        first.SetResult(new(DeviceConnectionStatus.Connected));
        second.SetResult(new(DeviceConnectionStatus.NoDevice));
        await refresh.Completion.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, calls);
        Assert.Equal(1, published);
        Assert.Equal(DeviceConnectionStatus.NoDevice, refresh.Current!.Status);
    }

    [Fact]
    public async Task Stop_AwaitsWorkAndNeverPublishesLateResults()
    {
        var pending = new TaskCompletionSource<DeviceDiscoveryResult>();
        CancellationToken token = default;
        var refresh = new DeviceRefreshController(new Discovery(cancellation => { token = cancellation; return pending.Task; }));
        refresh.Refresh();
        var stop = refresh.StopAsync();
        Assert.True(token.IsCancellationRequested);
        Assert.False(stop.IsCompleted);
        pending.SetResult(new(DeviceConnectionStatus.Connected));
        await stop.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Null(refresh.Current);
        refresh.Refresh();
        Assert.Null(refresh.Current);
    }

    [Fact]
    public async Task UnexpectedErrors_AreGenericAndRecoverable()
    {
        var fail = true;
        var refresh = new DeviceRefreshController(new Discovery(_ => fail
            ? Task.FromException<DeviceDiscoveryResult>(new InvalidOperationException("synthetic-private-id"))
            : Task.FromResult(new DeviceDiscoveryResult(DeviceConnectionStatus.NoDevice))));
        refresh.Refresh();
        await refresh.Completion;
        Assert.Equal(DeviceConnectionStatus.UnknownFailure, refresh.Current!.Status);
        Assert.DoesNotContain("synthetic-private-id", DevicePanel.Describe(refresh.Current));
        fail = false;
        refresh.Refresh();
        await refresh.Completion;
        Assert.Equal("No USB device connected.", DevicePanel.Describe(refresh.Current!));
    }

    [Fact]
    public void UiProjection_ShowsOnlyApprovedMetadataAndGenericUnavailableState()
    {
        var device = new DeviceInfo(DeviceConnectionStatus.Connected, DeviceTrustState.Trusted,
            "Synthetic phone", "iPhoneTest,1", "27.0", "TESTBUILD");
        var text = DevicePanel.Describe(new(DeviceConnectionStatus.Connected, [device]));
        Assert.Contains("Name: Synthetic phone", text);
        Assert.Contains("iOS: 27.0", text);
        Assert.Contains("Trust: Trusted", text);
        Assert.Contains("Native dependencies unavailable", DevicePanel.Describe(new(DeviceConnectionStatus.NativeDependencyUnavailable,
            diagnostic: DeviceDiagnostic.MissingNativeBundle)));
    }

    [Fact]
    public void DomainSnapshot_IsImmutableAndRejectsUnsafeMetadata()
    {
        var original = new List<DeviceInfo> { new(DeviceConnectionStatus.Connected, DeviceTrustState.Unknown) };
        var result = new DeviceDiscoveryResult(DeviceConnectionStatus.Connected, original);
        original.Clear();
        Assert.Single(result.Devices);
        Assert.Throws<ArgumentException>(() => new DeviceInfo(DeviceConnectionStatus.Connected, DeviceTrustState.NotTrusted, "private"));
        Assert.Throws<ArgumentException>(() => new DeviceInfo(DeviceConnectionStatus.Connected, DeviceTrustState.Trusted, "name\ncontrol"));
    }

    private sealed class Discovery(Func<CancellationToken, Task<DeviceDiscoveryResult>> discover) : IDeviceDiscovery
    {
        public Task<DeviceDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default) => discover(cancellationToken);
    }
}
