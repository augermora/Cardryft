using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using Cardryft.Imaging;

namespace Cardryft.Tests;

public sealed class ImageLoaderTests
{
    [Theory]
    [InlineData("synthetic.png")]
    [InlineData("synthetic.jpg")]
    [InlineData("synthetic.jpeg")]
    [InlineData("synthetic.PNG")]
    public void Load_SupportsExpectedFormatsAndReleasesSourceFile(string name)
    {
        using var files = new ImageTestFiles();
        var path = files.CreateImage(name);
        using var image = new ImageLoader().Load(path);
        Assert.Equal(300, image.Width);
        Assert.Equal(100, image.Height);
        using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(exclusive.CanWrite);
        }
        File.Delete(path);
        using var rendered = new ArtworkRenderer().Render(image, new Core.ArtworkSession(path), TestContext.Current.CancellationToken);
        Assert.Equal(1024, rendered.Width);
    }

    [Theory]
    [InlineData("not-an-image.png")]
    [InlineData("not-an-image.jpg")]
    [InlineData("unsupported.bmp")]
    [InlineData("unsupported.exe")]
    public void Load_RejectsInvalidOrUnsupportedContent(string name)
    {
        using var files = new ImageTestFiles();
        var path = files.PathFor(name);
        File.WriteAllText(path, "Synthetic invalid content. This is not an executable.");
        Assert.Throws<InvalidDataException>(() => new ImageLoader().Load(path));
    }

    [Fact]
    public void Load_RejectsOtherRasterFormatRenamedToPng()
    {
        using var files = new ImageTestFiles();
        var path = files.PathFor("renamed.png");
        using (var bitmap = new Bitmap(20, 20)) bitmap.Save(path, ImageFormat.Gif);
        Assert.Throws<InvalidDataException>(() => new ImageLoader().Load(path));
    }

    [Fact]
    public void Load_RejectsTruncatedPngWithValidDimensionHeader()
    {
        using var files = new ImageTestFiles();
        var path = files.CreateImage("truncated.png");
        File.WriteAllBytes(path, File.ReadAllBytes(path)[..24]);
        Assert.Throws<InvalidDataException>(() => new ImageLoader().Load(path));
    }

    [Theory]
    [InlineData(8193, 100)]
    [InlineData(8192, 8192)]
    [InlineData(0, 100)]
    public void Load_RejectsUnsafeDimensionsBeforeDecoding(int width, int height)
    {
        using var files = new ImageTestFiles();
        var path = files.CreateImage("large-header.png");
        var bytes = File.ReadAllBytes(path);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        File.WriteAllBytes(path, bytes);
        Assert.Throws<InvalidDataException>(() => new ImageLoader().Load(path));
    }

    [Fact]
    public void Load_RejectsOversizedEncodedFile()
    {
        using var files = new ImageTestFiles();
        var path = files.PathFor("oversized.png");
        using (var stream = File.Create(path)) stream.SetLength(ImageLoader.MaximumFileBytes + 1);
        Assert.Throws<InvalidDataException>(() => new ImageLoader().Load(path));
    }

    [Fact]
    public void Load_RejectsMissingLocalFile()
    {
        using var files = new ImageTestFiles();
        Assert.Throws<FileNotFoundException>(() => new ImageLoader().Load(files.PathFor("missing.png")));
    }

    [Fact]
    public void Load_CopiesPixelsWithoutPhysicalDpiScaling()
    {
        using var files = new ImageTestFiles();
        var renderer = new ArtworkRenderer();
        var pixelSnapshots = new List<int[]>();
        foreach (var dpi in new[] { 72, 144, 300 })
        {
            var path = files.PathFor($"dpi-{dpi}.png");
            using (var bitmap = new Bitmap(300, 100))
            {
                bitmap.SetResolution(dpi, dpi);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.Red);
                graphics.FillRectangle(Brushes.Blue, 150, 0, 150, 100);
                bitmap.Save(path, ImageFormat.Png);
            }
            using var source = new ImageLoader().Load(path);
            using var rendered = renderer.Render(source, new Core.ArtworkSession(path, new Core.ArtworkSize(300, 100)), TestContext.Current.CancellationToken);
            Assert.Equal(Color.Red.ToArgb(), rendered.GetPixel(140, 50).ToArgb());
            Assert.Equal(Color.Blue.ToArgb(), rendered.GetPixel(160, 50).ToArgb());
            Assert.Equal(96, rendered.HorizontalResolution);
            pixelSnapshots.Add(Enumerable.Range(0, 300).Select(x => rendered.GetPixel(x, 50).ToArgb()).ToArray());
        }
        Assert.Equal(pixelSnapshots[0], pixelSnapshots[1]);
        Assert.Equal(pixelSnapshots[0], pixelSnapshots[2]);
    }

    [Fact]
    public void Load_NormalizesJpegExifOrientation()
    {
        using var files = new ImageTestFiles();
        var path = files.CreateImage("oriented.jpg");
        var jpeg = File.ReadAllBytes(path);
        var exif = new byte[32];
        "Exif\0\0"u8.CopyTo(exif);
        exif[6] = exif[7] = (byte)'I';
        exif[8] = 42;
        exif[10] = 8;
        exif[14] = 1;
        exif[16] = 0x12;
        exif[17] = 0x01;
        exif[18] = 3;
        exif[20] = 1;
        exif[24] = 6; // rotate clockwise 90 degrees
        var bytes = jpeg[..2].Concat(new byte[] { 0xff, 0xe1, 0, 34 }).Concat(exif).Concat(jpeg[2..]).ToArray();
        File.WriteAllBytes(path, bytes);
        using var image = new ImageLoader().Load(path);
        Assert.Equal(100, image.Width);
        Assert.Equal(300, image.Height);
    }

    [Fact]
    public void Load_RejectsNetworkPathBeforeAccessingIt()
    {
        Assert.Throws<NotSupportedException>(() => new ImageLoader().Load(@"\\invalid-host\share\synthetic.png"));
    }
}
