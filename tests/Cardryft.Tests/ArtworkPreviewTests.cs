using System.Drawing;
using System.Runtime.ExceptionServices;
using Cardryft.App;
using Cardryft.Core;
using Cardryft.Imaging;

namespace Cardryft.Tests;

public sealed class ArtworkPreviewTests
{
    [Fact]
    public void RoundedPresentation_DoesNotChangeArtworkOrExportAfterResize()
    {
        // WinForms controls require an STA thread; exercise the production control directly.
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var files = new ImageTestFiles();
                using var source = new ImageLoader().Load(files.CreateImage("source.png", 300, 300, graphics =>
                {
                    graphics.Clear(Color.CornflowerBlue);
                    graphics.FillRectangle(Brushes.Red, 0, 0, 150, 300);
                }));
                var renderer = new ArtworkRenderer();
                var session = new ArtworkSession(source.Path, transform: new ArtworkTransform(2, 0.25, -0.5));
                using var artwork = renderer.Render(source, session, TestContext.Current.CancellationToken);
                var beforePath = files.PathFor("before.png");
                var afterPath = files.PathFor("after.png");
                renderer.ExportPng(source, session, beforePath);
                using var preview = new ArtworkPreview { Artwork = artwork };
                foreach (var size in new[] { new Size(576, 384), new Size(896, 600) })
                {
                    preview.Size = size;
                    using var display = new Bitmap(size.Width, size.Height);
                    preview.DrawToBitmap(display, new Rectangle(Point.Empty, size));
                    var cardWidth = size.Width - 64;
                    var cardHeight = cardWidth * 640 / 1024;
                    var left = 32;
                    var top = (size.Height - cardHeight) / 2;
                    Assert.Equal(preview.BackColor.ToArgb(), display.GetPixel(left, top).ToArgb());
                    Assert.NotEqual(preview.BackColor.ToArgb(), display.GetPixel(size.Width / 2, size.Height / 2).ToArgb());
                    Assert.Equal(255, artwork.GetPixel(0, 0).A);
                }

                renderer.ExportPng(source, session, afterPath);
                Assert.Equal(File.ReadAllBytes(beforePath), File.ReadAllBytes(afterPath));
                using var exported = new Bitmap(afterPath);
                Assert.Equal(1024, exported.Width);
                Assert.Equal(640, exported.Height);
                for (var y = 0; y < artwork.Height; y++)
                {
                    for (var x = 0; x < artwork.Width; x++)
                    {
                        Assert.Equal(exported.GetPixel(x, y).ToArgb(), artwork.GetPixel(x, y).ToArgb());
                    }
                }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
