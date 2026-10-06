using System.Drawing;
using System.Drawing.Imaging;

namespace Cardryft.Tests;

internal sealed class ImageTestFiles : IDisposable
{
    private readonly string testRoot;
    public string DirectoryPath { get; }

    public ImageTestFiles()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(repository.FullName, "Cardryft.sln")))
        {
            repository = repository.Parent ?? throw new InvalidOperationException("Repository root not found.");
        }

        testRoot = Path.Combine(repository.FullName, ".local", "tests");
        DirectoryPath = Path.Combine(testRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string PathFor(string name) => Path.Combine(DirectoryPath, name);

    public string CreateImage(string name, int width = 300, int height = 100, Action<Graphics>? paint = null)
    {
        var path = PathFor(name);
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.CornflowerBlue);
            paint?.Invoke(graphics);
        }
        bitmap.Save(path, Path.GetExtension(name).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? ImageFormat.Png : ImageFormat.Jpeg);
        return path;
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(DirectoryPath);
        if (!resolved.StartsWith(Path.GetFullPath(testRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test cleanup escaped its workspace directory.");
        }
        Directory.Delete(resolved, recursive: true);
    }
}
