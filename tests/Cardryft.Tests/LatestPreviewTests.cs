using System.Drawing;
using Cardryft.App;
using Cardryft.Core;

namespace Cardryft.Tests;

public sealed class LatestPreviewTests
{
    [Fact]
    public async Task NewestRequestWins_PendingRequestsCoalesce_StaleBitmapIsDisposed()
    {
        var first = new TaskCompletionSource<Bitmap>();
        var second = new TaskCompletionSource<Bitmap>();
        var calls = new List<double>();
        CancellationToken firstToken = default;
        var preview = new LatestPreview((session, token) =>
        {
            calls.Add(session.Transform.Zoom);
            if (calls.Count == 1) { firstToken = token; return first.Task; }
            return second.Task;
        });
        Bitmap? accepted = null;
        preview.Ready += bitmap => accepted = bitmap;
        preview.Request(new ArtworkSession("source.png"));
        preview.Request(new ArtworkSession("source.png", transform: new ArtworkTransform(2, 0, 0)));
        preview.Request(new ArtworkSession("source.png", transform: new ArtworkTransform(3, 0, 0)));
        Assert.True(firstToken.IsCancellationRequested);
        var stale = new Bitmap(10, 10);
        first.SetResult(stale); // Simulates a native render that ignores cancellation and completes late.
        var latest = new Bitmap(10, 10);
        second.SetResult(latest);
        await preview.Completion.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 1d, 3d }, calls);
        Assert.Same(latest, accepted);
        Assert.Throws<ArgumentException>(() => stale.GetPixel(0, 0));
        accepted!.Dispose();
    }

    [Fact]
    public async Task StopCancelsAndAwaitsActiveWork_AndSuppressesStaleErrors()
    {
        var rendering = new TaskCompletionSource<Bitmap>();
        CancellationToken token = default;
        var preview = new LatestPreview((_, cancellation) => { token = cancellation; return rendering.Task; });
        var ready = false;
        var failed = false;
        preview.Ready += _ => ready = true;
        preview.Failed += _ => failed = true;
        preview.Request(new ArtworkSession("source.png"));
        var stop = preview.StopAsync();
        Assert.True(token.IsCancellationRequested);
        Assert.False(stop.IsCompleted);
        rendering.SetException(new InvalidDataException("stale error"));
        await stop.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(ready);
        Assert.False(failed);
    }

    [Fact]
    public async Task CurrentFailureIsReported_AndNextRequestCanRecover()
    {
        var shouldFail = true;
        var preview = new LatestPreview((_, _) => shouldFail
            ? Task.FromException<Bitmap>(new InvalidDataException("render failed"))
            : Task.FromResult(new Bitmap(10, 10)));
        Exception? failure = null;
        Bitmap? accepted = null;
        preview.Failed += error => failure = error;
        preview.Ready += bitmap => accepted = bitmap;
        preview.Request(new ArtworkSession("source.png"));
        await preview.Completion;
        Assert.IsType<InvalidDataException>(failure);
        shouldFail = false;
        preview.Request(new ArtworkSession("source.png"));
        await preview.Completion;
        Assert.NotNull(accepted);
        accepted.Dispose();
    }
}
