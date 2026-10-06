using System.ComponentModel;
using System.Drawing.Drawing2D;
using Cardryft.Core;

namespace Cardryft.App;

internal sealed class ArtworkPreview : Control
{
    private const float CornerRadiusRatio = 1f / 16;

    // The editor owns the rectangular bitmap; presentation never modifies its pixels.
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Bitmap? Artwork { get; set; }

    public ArtworkPreview()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(237, 239, 242);
        AccessibleName = "Artwork card preview";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var size = Artwork is null ? new Size(ArtworkSize.Canonical.Width, ArtworkSize.Canonical.Height) : Artwork.Size;
        var scale = Math.Min((float)Math.Max(1, ClientSize.Width - 64) / size.Width,
            (float)Math.Max(1, ClientSize.Height - 64) / size.Height);
        var bounds = new RectangleF((ClientSize.Width - size.Width * scale) / 2,
            (ClientSize.Height - size.Height * scale) / 2, size.Width * scale, size.Height * scale);

        using var path = new GraphicsPath();
        var diameter = bounds.Height * CornerRadiusRatio * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Artwork is not null)
        {
            using var brush = new TextureBrush(Artwork, WrapMode.TileFlipXY);
            using var transform = new Matrix(bounds.Width / Artwork.Width, 0, 0,
                bounds.Height / Artwork.Height, bounds.X, bounds.Y);
            brush.Transform = transform;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.FillPath(brush, path);
            return;
        }

        using var fill = new SolidBrush(Color.FromArgb(248, 249, 251));
        using var border = new Pen(Color.FromArgb(209, 214, 222));
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        TextRenderer.DrawText(e.Graphics, "Import a PNG or JPEG to begin", Font,
            Rectangle.Round(bounds), Color.FromArgb(96, 105, 119),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
