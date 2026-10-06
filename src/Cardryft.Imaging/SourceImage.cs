using System.Drawing;

namespace Cardryft.Imaging;

/// <summary>Owns detached pixels; importing leaves no source file handle open.</summary>
public sealed class SourceImage : IDisposable
{
    internal SourceImage(string path, Bitmap pixels)
    {
        Path = path;
        Pixels = pixels;
    }

    public string Path { get; }
    public int Width => Pixels.Width;
    public int Height => Pixels.Height;
    internal Bitmap Pixels { get; }

    public void Dispose() => Pixels.Dispose();
}
