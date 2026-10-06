using Cardryft.Core;

namespace Cardryft.Tests;

public sealed class ArtworkSessionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(2.35)]
    public void Transform_AcceptsZoomWithinBoundaries(double zoom)
    {
        Assert.Equal(zoom, new ArtworkTransform(zoom, 0, 0).Zoom);
    }

    [Theory]
    [InlineData(0.99)]
    [InlineData(4.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Transform_RejectsInvalidZoom(double zoom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArtworkTransform(zoom, 0, 0));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(0.25, -0.5)]
    public void Transform_PreservesSignedOffsets(double horizontal, double vertical)
    {
        var transform = new ArtworkTransform(2, horizontal, vertical);
        Assert.Equal(horizontal, transform.HorizontalOffset);
        Assert.Equal(vertical, transform.VerticalOffset);
    }

    [Theory]
    [InlineData(-1.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Transform_RejectsInvalidOffsetsOnBothAxes(double offset)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArtworkTransform(1, offset, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArtworkTransform(1, 0, offset));
    }

    [Fact]
    public void Session_DefaultsToCanonicalCenteredCover()
    {
        var session = new ArtworkSession("synthetic.png");
        Assert.Equal(1024, session.OutputWidth);
        Assert.Equal(640, session.OutputHeight);
        Assert.Equal(ArtworkTransform.Default, session.Transform);
    }

    [Fact]
    public void Reset_PreservesSourceAndOutputWithoutChangingOriginal()
    {
        var original = new ArtworkSession("synthetic.png", new ArtworkSize(320, 200), new ArtworkTransform(3, -1, 0.6));
        var reset = original.ResetTransform();
        Assert.Equal(ArtworkTransform.Default, reset.Transform);
        Assert.Equal(original.SourcePath, reset.SourcePath);
        Assert.Equal(original.OutputSize, reset.OutputSize);
        Assert.Equal(3, original.Transform.Zoom);
        Assert.Equal(-1, original.Transform.HorizontalOffset);
        Assert.Equal(0.6, original.Transform.VerticalOffset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Session_RejectsMissingSource(string? path)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ArtworkSession(path!));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(200, 0)]
    [InlineData(4097, 640)]
    [InlineData(200, 4097)]
    [InlineData(100, 200)]
    [InlineData(100, 100)]
    public void Size_RejectsUnsafeOrNonLandscapeDimensions(int width, int height)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ArtworkSize(width, height));
    }

    [Fact]
    public void Session_RejectsNullTransformReplacement()
    {
        Assert.Throws<ArgumentNullException>(() => new ArtworkSession("synthetic.png").WithTransform(null!));
    }
}
