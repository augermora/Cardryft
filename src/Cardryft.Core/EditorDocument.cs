namespace Cardryft.Core;

/// <summary>Value-only history and saved-state tracking; no images or filesystem access.</summary>
public sealed class EditorDocument
{
    public const int HistoryLimit = 100;
    private readonly List<ArtworkTransform> undo = [];
    private readonly List<ArtworkTransform> redo = [];
    private ArtworkSession? saved;
    public ArtworkSession? Session { get; private set; }
    public string? ProjectPath { get; private set; }
    public bool IsDirty => Session != saved;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;

    public void New() => Open(null, null);

    public void Open(ArtworkSession? session, string? projectPath)
    {
        Session = saved = session;
        ProjectPath = projectPath;
        undo.Clear();
        redo.Clear();
    }

    public void Import(ArtworkSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Session = session;
        undo.Clear();
        redo.Clear();
    }

    public void MarkSaved(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Session is null) throw new InvalidOperationException("There is no artwork to save.");
        ProjectPath = path;
        saved = Session;
    }

    public void Apply(ArtworkTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        if (Session is null || Session.Transform == transform) return;
        Push(undo, Session.Transform);
        redo.Clear();
        Session = Session.WithTransform(transform);
    }

    public void Undo()
    {
        if (Session is null || !CanUndo) return;
        Push(redo, Session.Transform);
        Session = Session.WithTransform(Pop(undo));
    }

    public void Redo()
    {
        if (Session is null || !CanRedo) return;
        Push(undo, Session.Transform);
        Session = Session.WithTransform(Pop(redo));
    }

    private static void Push(List<ArtworkTransform> stack, ArtworkTransform transform)
    {
        stack.Add(transform);
        if (stack.Count > HistoryLimit) stack.RemoveAt(0);
    }

    private static ArtworkTransform Pop(List<ArtworkTransform> stack)
    {
        var value = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return value;
    }
}
