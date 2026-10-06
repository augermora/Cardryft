namespace Cardryft.Core;

public sealed record ArtworkSize
{
    // Provisional Cardryft editor/export size; replace after future device/Wallet research.
    public static ArtworkSize Canonical { get; } = new(1024, 640);
    public const int MinimumDimension = 64;
    public const int MaximumDimension = 4096;

    public ArtworkSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, MinimumDimension);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaximumDimension);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, MinimumDimension);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, MaximumDimension);
        if (width <= height)
        {
            throw new ArgumentException("Artwork must have a landscape aspect ratio.", nameof(width));
        }

        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
}
