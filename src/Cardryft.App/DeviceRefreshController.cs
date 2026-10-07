using Cardryft.Core;

namespace Cardryft.App;

// Owned by the UI context: one in-flight query and one coalesced pending refresh.
internal sealed class DeviceRefreshController(IDeviceDiscovery discovery)
{
    private bool pending;
    private bool running;
    private bool stopped;
    private long revision;
    private CancellationTokenSource? cancellation;
    public Task Completion { get; private set; } = Task.CompletedTask;
    public DeviceDiscoveryResult? Current { get; private set; }
    public event Action? Changed;

    public void Refresh()
    {
        if (stopped) return;
        pending = true;
        revision++;
        cancellation?.Cancel();
        if (running) return;
        running = true;
        Completion = PumpAsync();
    }

    public async Task StopAsync()
    {
        stopped = true;
        pending = false;
        revision++;
        cancellation?.Cancel();
        await Completion;
    }

    private async Task PumpAsync()
    {
        try
        {
            while (pending && !stopped)
            {
                pending = false;
                var request = revision;
                using var current = new CancellationTokenSource();
                cancellation = current;
                DeviceDiscoveryResult? result = null;
                try { result = await discovery.DiscoverAsync(current.Token); }
                catch (OperationCanceledException) when (current.IsCancellationRequested) { }
                catch (Exception) { result = new(DeviceConnectionStatus.UnknownFailure, diagnostic: DeviceDiagnostic.UnknownFailure); }
                if (result is not null && request == revision && !current.IsCancellationRequested && !stopped)
                {
                    Current = result;
                    Changed?.Invoke();
                }
                cancellation = null;
            }
        }
        finally { cancellation = null; running = false; }
    }
}
