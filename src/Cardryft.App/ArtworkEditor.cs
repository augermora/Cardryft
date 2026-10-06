using Cardryft.Core;
using Cardryft.Imaging;
using Cardryft.Storage;

namespace Cardryft.App;

/// <summary>UI-context orchestration, async work, and decoded image ownership.</summary>
internal sealed class ArtworkEditor : IDisposable
{
    private readonly ImageLoader loader;
    private readonly ArtworkRenderer renderer;
    private readonly ProjectStore projects;
    private readonly RecentProjects recent;
    private readonly LatestPreview previews;
    private readonly CancellationTokenSource lifetime = new();
    private SourceImage? source;
    private Task operation = Task.CompletedTask;
    public EditorDocument Document { get; } = new();
    public ArtworkSession? Session => Document.Session;
    public Bitmap? Preview { get; private set; }
    public bool IsBusy { get; private set; }
    public bool HasSource => source is not null;
    public string? Warning { get; private set; }
    public event Action? Changed;
    public event Action<Exception>? RenderFailed;

    public ArtworkEditor(ImageLoader loader, ArtworkRenderer renderer, ProjectStore projects, RecentProjects recent)
    {
        this.loader = loader;
        this.renderer = renderer;
        this.projects = projects;
        this.recent = recent;
        previews = new LatestPreview((session, token) => Task.Run(() => renderer.Render(source!, session, token), token));
        previews.Ready += bitmap =>
        {
            var previous = Preview;
            Preview = bitmap;
            Changed?.Invoke();
            previous?.Dispose();
        };
        previews.Failed += exception => RenderFailed?.Invoke(exception);
    }

    public IReadOnlyList<string> RecentPaths => recent.Read();

    public Task ImportAsync(string path) => StartOperation(async () =>
    {
        await previews.StopAsync();
        var imported = await Task.Run(() => loader.Load(path), lifetime.Token);
        try
        {
            lifetime.Token.ThrowIfCancellationRequested();
            var session = source is null && Session is not null
                ? new ArtworkSession(imported.Path, Session.OutputSize, Session.Transform)
                : new ArtworkSession(imported.Path);
            ReplaceSource(imported);
            imported = null!;
            Document.Import(session);
            Warning = null;
            RequestPreview();
        }
        finally { imported?.Dispose(); }
    });

    public Task OpenAsync(string path) => StartOperation(async () =>
    {
        var session = await Task.Run(() => projects.Load(path), lifetime.Token);
        await previews.StopAsync();
        SourceImage? imported = null;
        string? warning = null;
        try { imported = await Task.Run(() => loader.Load(session.SourcePath), lifetime.Token); }
        catch (Exception exception) when (IsFileError(exception))
        { warning = "Source image is missing or unreadable. Project state was retained; Import Image selects a replacement."; }
        try
        {
            lifetime.Token.ThrowIfCancellationRequested();
            ReplaceSource(imported);
            imported = null;
            Document.Open(session, ProjectStore.ProjectPath(path));
            Warning = warning;
            Remember(Document.ProjectPath!);
            RequestPreview();
        }
        finally { imported?.Dispose(); }
    });

    public Task NewAsync() => StartOperation(async () =>
    {
        await previews.StopAsync();
        ReplaceSource(null);
        Document.New();
        Warning = null;
    });

    public void Save(string path)
    {
        if (Session is null) throw new InvalidOperationException("Import artwork first.");
        projects.Save(path, Session);
        Document.MarkSaved(ProjectStore.ProjectPath(path));
        Warning = null;
        Remember(Document.ProjectPath!);
        Changed?.Invoke();
    }

    public void SetTransform(ArtworkTransform transform)
    {
        if (IsBusy) return;
        Document.Apply(transform);
        RequestPreview();
    }
    public void Reset() => SetTransform(ArtworkTransform.Default);
    public void Undo() { if (IsBusy) return; Document.Undo(); RequestPreview(); }
    public void Redo() { if (IsBusy) return; Document.Redo(); RequestPreview(); }

    public Task ExportAsync(string path) => StartOperation(async () =>
    {
        await previews.StopAsync();
        if (source is null || Session is null) throw new InvalidOperationException("Select a readable source image first.");
        renderer.ExportPng(source, Session, path);
    });

    public async Task ShutdownAsync()
    {
        lifetime.Cancel();
        await previews.StopAsync();
        try { await operation; }
        catch (OperationCanceledException) { }
        catch (Exception exception) when (IsFileError(exception)) { }
    }

    private Task StartOperation(Func<Task> action)
    {
        if (IsBusy) throw new InvalidOperationException("Another file operation is in progress.");
        operation = RunAsync();
        return operation;
        async Task RunAsync()
        {
            IsBusy = true;
            Changed?.Invoke();
            try { await action(); }
            catch { if (!lifetime.IsCancellationRequested) RequestPreview(); throw; }
            finally { IsBusy = false; Changed?.Invoke(); }
        }
    }

    private void RequestPreview()
    {
        if (source is not null && Session is not null) previews.Request(Session);
        Changed?.Invoke();
    }

    private void ReplaceSource(SourceImage? replacement)
    {
        var oldPreview = Preview;
        Preview = null;
        Changed?.Invoke();
        oldPreview?.Dispose();
        source?.Dispose();
        source = replacement;
    }

    private void Remember(string path)
    {
        try { recent.Add(path); }
        catch (Exception exception) when (IsFileError(exception))
        {
            const string recentWarning = "Project opened/saved, but the local recent list could not be updated.";
            Warning = Warning is null ? recentWarning : $"{Warning} {recentWarning}";
        }
    }

    internal static bool IsFileError(Exception exception) => exception is IOException or InvalidDataException or
        UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.ExternalException or
        OutOfMemoryException or NotSupportedException;

    public void Dispose()
    {
        Preview?.Dispose();
        source?.Dispose();
        lifetime.Dispose();
    }
}
