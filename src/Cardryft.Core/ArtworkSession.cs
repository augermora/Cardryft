namespace Cardryft.Core;

public sealed record ArtworkSession
{
    public ArtworkSession(string sourcePath, ArtworkSize? outputSize = null, ArtworkTransform? transform = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        SourcePath = sourcePath;
        OutputSize = outputSize ?? ArtworkSize.Canonical;
        Transform = transform ?? ArtworkTransform.Default;
    }

    public string SourcePath { get; }
    public ArtworkSize OutputSize { get; }
    public ArtworkTransform Transform { get; }
    public int OutputWidth => OutputSize.Width;
    public int OutputHeight => OutputSize.Height;

    public ArtworkSession WithTransform(ArtworkTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return new ArtworkSession(SourcePath, OutputSize, transform);
    }

    public ArtworkSession ResetTransform() => WithTransform(ArtworkTransform.Default);
}
