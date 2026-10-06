using Cardryft.Core;

namespace Cardryft.App;

/// <summary>Owned by the UI context. One worker and one replaceable pending request.</summary>
internal sealed class LatestPreview(Func<ArtworkSession, CancellationToken, Task<Bitmap>> render)
{
    private ArtworkSession? pending;
    private CancellationTokenSource? cancellation;
    private long revision;
    private bool running;
    public Task Completion { get; private set; } = Task.CompletedTask;
    public event Action<Bitmap>? Ready;
    public event Action<Exception>? Failed;

    public void Request(ArtworkSession session)
    {
        pending = session;
        revision++;
        cancellation?.Cancel();
        if (running) return;
        running = true;
        Completion = PumpAsync();
    }

    public async Task StopAsync()
    {
        pending = null;
        revision++;
        cancellation?.Cancel();
        await Completion;
    }

    private async Task PumpAsync()
    {
        try
        {
            while (pending is not null)
            {
                var session = pending;
                pending = null;
                var request = revision;
                using var current = new CancellationTokenSource();
                cancellation = current;
                Bitmap? bitmap = null;
                try
                {
                    bitmap = await render(session, current.Token);
                    if (request == revision && !current.IsCancellationRequested && Ready is not null)
                    {
                        Ready(bitmap);
                        bitmap = null;
                    }
                }
                catch (OperationCanceledException) when (current.IsCancellationRequested) { }
                catch (Exception exception)
                {
                    if (request == revision && !current.IsCancellationRequested) Failed?.Invoke(exception);
                }
                finally { bitmap?.Dispose(); cancellation = null; }
            }
        }
        finally { running = false; }
    }
}
