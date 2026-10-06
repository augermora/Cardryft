using System.Drawing;
using System.Drawing.Imaging;

namespace Cardryft.Tests;

public sealed class ImageTestFilesTests
{
    [Theory]
    [InlineData("concurrent.png")]
    [InlineData("concurrent.jpg")]
    public async Task ConcurrentFixtures_AreIndependentDeterministicAndReleaseFiles(string name)
    {
        const int workerCount = 16;
        var directories = new string[workerCount];
        var encoded = new byte[workerCount][];
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        var workers = Enumerable.Range(0, workerCount).Select(index => Task.Run(async () =>
        {
            if (Interlocked.Increment(ref arrived) == workerCount) ready.SetResult();
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            using var files = new ImageTestFiles();
            directories[index] = files.DirectoryPath;
            var path = files.CreateImage(name, 37, 23, graphics => graphics.Clear(Color.Red));
            using (var image = new Bitmap(path))
            {
                Assert.Equal(37, image.Width);
                Assert.Equal(23, image.Height);
                Assert.Equal(name.EndsWith(".png", StringComparison.Ordinal)
                    ? ImageFormat.Png.Guid : ImageFormat.Jpeg.Guid, image.RawFormat.Guid);
                var pixel = image.GetPixel(18, 11);
                Assert.InRange(pixel.R, 250, 255);
                Assert.InRange(pixel.G, 0, 5);
                Assert.InRange(pixel.B, 0, 5);
            }
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                encoded[index] = new byte[checked((int)stream.Length)];
                stream.ReadExactly(encoded[index]);
            }
            File.Delete(path);
            Assert.False(File.Exists(path));
        })).ToArray();
        await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
        start.SetResult();
        await Task.WhenAll(workers);
        Assert.Equal(workerCount, directories.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var bytes in encoded) Assert.Equal(encoded[0], bytes);
        foreach (var directory in directories) Assert.False(Directory.Exists(directory));
    }
}
