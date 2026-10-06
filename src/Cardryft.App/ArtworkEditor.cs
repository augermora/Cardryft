using Cardryft.Core;
using Cardryft.Imaging;

namespace Cardryft.App;

/// <summary>Coordinates state and image ownership; UI handlers only delegate user actions.</summary>
internal sealed class ArtworkEditor(ImageLoader loader, ArtworkRenderer renderer) : IDisposable
{
    private SourceImage? source;
    public ArtworkSession? Session { get; private set; }
    public Bitmap? Preview { get; private set; }

    public void Import(string path)
    {
        var imported = loader.Load(path);
        try
        {
            var session = new ArtworkSession(imported.Path);
            var preview = renderer.Render(imported, session);
            Preview?.Dispose();
            source?.Dispose();
            source = imported;
            Session = session;
            Preview = preview;
        }
        catch
        {
            imported.Dispose();
            throw;
        }
    }

    public void SetTransform(ArtworkTransform transform)
    {
        if (source is null || Session is null) return;
        var session = Session.WithTransform(transform);
        var preview = renderer.Render(source, session);
        Preview?.Dispose();
        Session = session;
        Preview = preview;
    }

    public void Reset() => SetTransform(ArtworkTransform.Default);

    public void Export(string path)
    {
        if (source is null || Session is null) throw new InvalidOperationException("Import an image first.");
        renderer.ExportPng(source, Session, path);
    }

    public void Dispose()
    {
        Preview?.Dispose();
        source?.Dispose();
    }
}
