using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Cardryft.Core;

namespace Cardryft.Imaging;

public sealed class ArtworkRenderer
{
    public Bitmap Render(SourceImage source, ArtworkSession session)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(session);
        var width = session.OutputWidth;
        var height = session.OutputHeight;
        var scale = Math.Max((double)width / source.Width, (double)height / source.Height) * session.Transform.Zoom;
        var scaledWidth = source.Width * scale;
        var scaledHeight = source.Height * scale;
        var left = (width - scaledWidth) / 2 + session.Transform.HorizontalOffset * (scaledWidth - width) / 2;
        var top = (height - scaledHeight) / 2 + session.Transform.VerticalOffset * (scaledHeight - height) / 2;

        var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try
        {
            output.SetResolution(96, 96);
            using var graphics = Graphics.FromImage(output);
            using var attributes = new ImageAttributes();
            graphics.Clear(Color.Transparent);
            graphics.PageUnit = GraphicsUnit.Pixel;
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(source.Pixels,
                new PointF[]
                {
                    new((float)left, (float)top),
                    new((float)(left + scaledWidth), (float)top),
                    new((float)left, (float)(top + scaledHeight)),
                },
                new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel, attributes);
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    public void ExportPng(SourceImage source, ArtworkSession session, string path)
    {
        var fullPath = LocalFilePath.Resolve(path);
        if (!Path.GetExtension(fullPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Artwork must be exported to a PNG file.", nameof(path));
        }

        using var artwork = Render(source, session);
        // Encode first, then replace the destination. Failed encoding leaves existing files intact.
        var temporaryPath = Path.Combine(Path.GetDirectoryName(fullPath)!, $".cardryft-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                artwork.Save(stream, ImageFormat.Png);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

}
