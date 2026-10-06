namespace Cardryft.Core;

/// <summary>Zoom relative to cover scale; offsets are fractions of the available crop travel.</summary>
public sealed record ArtworkTransform
{
    public const double MinimumZoom = 1;
    public const double MaximumZoom = 4;
    public static ArtworkTransform Default { get; } = new(1, 0, 0);

    public ArtworkTransform(double zoom, double horizontalOffset, double verticalOffset)
    {
        ValidateRange(zoom, MinimumZoom, MaximumZoom, nameof(zoom));
        ValidateRange(horizontalOffset, -1, 1, nameof(horizontalOffset));
        ValidateRange(verticalOffset, -1, 1, nameof(verticalOffset));
        Zoom = zoom;
        HorizontalOffset = horizontalOffset;
        VerticalOffset = verticalOffset;
    }

    public double Zoom { get; }
    public double HorizontalOffset { get; }
    public double VerticalOffset { get; }

    private static void ValidateRange(double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name, "The value is outside the supported range.");
        }
    }
}
