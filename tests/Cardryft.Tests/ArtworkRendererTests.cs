using System.Drawing;
using Cardryft.Core;
using Cardryft.Imaging;

namespace Cardryft.Tests;

public sealed class ArtworkRendererTests
{
    [Fact]
    public void Export_UsesCanonicalSizeAndDoesNotMaskAnyPixels()
    {
        using var files = new ImageTestFiles();
        using var source = new ImageLoader().Load(files.CreateImage("opaque.png", paint: graphics =>
            graphics.Clear(Color.CornflowerBlue)));
        var path = files.PathFor("rectangular.png");
        new ArtworkRenderer().ExportPng(source, new ArtworkSession(source.Path), path);
        using var exported = new Bitmap(path);
        Assert.Equal(1024, exported.Width);
        Assert.Equal(640, exported.Height);
        for (var y = 0; y < exported.Height; y++)
        {
            for (var x = 0; x < exported.Width; x++)
            {
                Assert.Equal(Color.CornflowerBlue.ToArgb(), exported.GetPixel(x, y).ToArgb());
            }
        }
    }

    [Fact]
    public void Render_UsesCanonicalDimensionsAndPreservesOpaqueCorners()
    {
        using var files = new ImageTestFiles();
        using var source = new ImageLoader().Load(files.CreateImage("source.png"));
        using var image = new ArtworkRenderer().Render(source, new ArtworkSession(source.Path));
        Assert.Equal(1024, image.Width);
        Assert.Equal(640, image.Height);
        Assert.Equal(255, image.GetPixel(0, 0).A);
        Assert.Equal(255, image.GetPixel(image.Width - 1, 0).A);
        Assert.Equal(255, image.GetPixel(0, image.Height - 1).A);
        Assert.Equal(255, image.GetPixel(image.Width - 1, image.Height - 1).A);
        Assert.Equal(255, image.GetPixel(512, 320).A);
    }

    [Fact]
    public void Render_PreservesTransparentSourcePixels()
    {
        using var files = new ImageTestFiles();
        using var source = new ImageLoader().Load(files.CreateImage("transparent.png", paint: graphics =>
            graphics.Clear(Color.FromArgb(128, 80, 120, 160))));
        using var image = new ArtworkRenderer().Render(source, new ArtworkSession(source.Path));
        Assert.Equal(128, image.GetPixel(512, 320).A);
        Assert.Equal(128, image.GetPixel(0, 0).A);
        Assert.Equal(128, image.GetPixel(image.Width - 1, image.Height - 1).A);
    }

    [Fact]
    public void Render_HorizontalPanChangesCropAndStopsAtSourceEdges()
    {
        using var files = new ImageTestFiles();
        using var source = new ImageLoader().Load(files.CreateImage("bands.png", paint: graphics =>
        {
            graphics.Clear(Color.Green);
            graphics.FillRectangle(Brushes.Red, 0, 0, 60, 100);
            graphics.FillRectangle(Brushes.Blue, 240, 0, 60, 100);
        }));
        var renderer = new ArtworkRenderer();
        var session = new ArtworkSession(source.Path, new ArtworkSize(200, 100));
        using var center = renderer.Render(source, session);
        using var right = renderer.Render(source, session.WithTransform(new ArtworkTransform(1, 1, 0)));
        using var left = renderer.Render(source, session.WithTransform(new ArtworkTransform(1, -1, 0)));
        Assert.Equal(Color.Green.ToArgb(), center.GetPixel(20, 50).ToArgb());
        Assert.Equal(Color.Red.ToArgb(), right.GetPixel(20, 50).ToArgb());
        Assert.Equal(Color.Green.ToArgb(), center.GetPixel(180, 50).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), left.GetPixel(180, 50).ToArgb());
        Assert.Equal(255, right.GetPixel(100, 50).A);
        Assert.Equal(255, left.GetPixel(100, 50).A);
    }

    [Fact]
    public void Render_VerticalPanChangesCrop()
    {
        using var files = new ImageTestFiles();
        using var source = new ImageLoader().Load(files.CreateImage("vertical.png", 200, 300, graphics =>
        {
            graphics.Clear(Color.Green);
            graphics.FillRectangle(Brushes.Red, 0, 0, 200, 60);
            graphics.FillRectangle(Brushes.Blue, 0, 240, 200, 60);
        }));
        var renderer = new ArtworkRenderer();
        var session = new ArtworkSession(source.Path, new ArtworkSize(200, 100));
        using var top = renderer.Render(source, session.WithTransform(new ArtworkTransform(1, 0, 1)));
        using var bottom = renderer.Render(source, session.WithTransform(new ArtworkTransform(1, 0, -1)));
        Assert.Equal(Color.Red.ToArgb(), top.GetPixel(100, 20).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), bottom.GetPixel(100, 80).ToArgb());
    }

    [Fact]
    public void Render_ZoomCropsOuterContent()
    {
        using var files = new ImageTestFiles();
        using var source = new ImageLoader().Load(files.CreateImage("zoom.png", 200, 100, graphics =>
        {
            graphics.Clear(Color.Red);
            graphics.FillRectangle(Brushes.Green, 60, 0, 80, 100);
        }));
        var renderer = new ArtworkRenderer();
        var session = new ArtworkSession(source.Path, new ArtworkSize(200, 100));
        using var initial = renderer.Render(source, session);
        using var zoomed = renderer.Render(source, session.WithTransform(new ArtworkTransform(4, 0, 0)));
        Assert.Equal(Color.Red.ToArgb(), initial.GetPixel(20, 50).ToArgb());
        Assert.Equal(Color.Green.ToArgb(), zoomed.GetPixel(20, 50).ToArgb());
    }

    [Fact]
    public void Export_MatchesPreviewPixelsAndIsDeterministicWithoutSourceMetadata()
    {
        using var files = new ImageTestFiles();
        using var source = new ImageLoader().Load(files.CreateImage("private-source-name.png"));
        var renderer = new ArtworkRenderer();
        var session = new ArtworkSession(source.Path, new ArtworkSize(200, 100), new ArtworkTransform(2, 0.5, -0.5));
        using var preview = renderer.Render(source, session);
        var firstPath = files.PathFor("first.png");
        var secondPath = files.PathFor("second.png");
        renderer.ExportPng(source, session, firstPath);
        renderer.ExportPng(source, session, secondPath);
        Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
        using var exported = new Bitmap(firstPath);
        Assert.Equal(200, exported.Width);
        Assert.Equal(100, exported.Height);
        for (var y = 0; y < preview.Height; y++)
        {
            for (var x = 0; x < preview.Width; x++)
            {
                Assert.Equal(preview.GetPixel(x, y).ToArgb(), exported.GetPixel(x, y).ToArgb());
            }
        }
        var encodedText = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(firstPath));
        Assert.DoesNotContain("private-source-name", encodedText);
        Assert.DoesNotContain(source.Path, encodedText);
    }

    [Fact]
    public void Reset_RestoresOriginalRenderAfterEditing()
    {
        using var files = new ImageTestFiles();
        using var source = new ImageLoader().Load(files.CreateImage("reset.png", paint: graphics =>
            graphics.FillRectangle(Brushes.Red, 0, 0, 60, 100)));
        var renderer = new ArtworkRenderer();
        var original = new ArtworkSession(source.Path, new ArtworkSize(200, 100));
        using var initial = renderer.Render(source, original);
        using var reset = renderer.Render(source, original.WithTransform(new ArtworkTransform(3, 1, -1)).ResetTransform());
        for (var y = 0; y < initial.Height; y++)
        {
            for (var x = 0; x < initial.Width; x++)
            {
                Assert.Equal(initial.GetPixel(x, y).ToArgb(), reset.GetPixel(x, y).ToArgb());
            }
        }
    }

    [Fact]
    public void Export_RejectsNonPngDestinationWithoutWriting()
    {
        using var files = new ImageTestFiles();
        using var source = new ImageLoader().Load(files.CreateImage("source.png"));
        var path = files.PathFor("artwork.jpg");
        Assert.Throws<ArgumentException>(() => new ArtworkRenderer().ExportPng(source, new ArtworkSession(source.Path), path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ExportFailure_PreservesExistingDestinationAndRemovesTemporaryFile()
    {
        using var files = new ImageTestFiles();
        using var source = new ImageLoader().Load(files.CreateImage("source.png"));
        var path = files.PathFor("locked.png");
        File.WriteAllText(path, "existing data");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = Record.Exception(() => new ArtworkRenderer().ExportPng(source, new ArtworkSession(source.Path), path));
            Assert.True(exception is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("existing data", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(files.DirectoryPath, ".cardryft-*.tmp"));
    }
}
