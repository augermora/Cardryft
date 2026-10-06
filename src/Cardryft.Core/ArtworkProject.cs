namespace Cardryft.Core;

/// <summary>A named artwork project containing no payment or device information.</summary>
public sealed class ArtworkProject
{
    public ArtworkProject(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public string Name { get; }
}
