using Cardryft.Core;

namespace Cardryft.Tests;

public sealed class EditorDocumentTests
{
    [Fact]
    public void DirtyState_FollowsSavedSnapshotIncludingUndoAndRedo()
    {
        var document = new EditorDocument();
        Assert.False(document.IsDirty);
        document.Import(new ArtworkSession("source.png"));
        Assert.True(document.IsDirty);
        document.MarkSaved("example.cardryft");
        Assert.False(document.IsDirty);
        document.Apply(new ArtworkTransform(2, 0.2, -0.3));
        Assert.True(document.IsDirty);
        document.Undo();
        Assert.Equal(ArtworkTransform.Default, document.Session!.Transform);
        Assert.False(document.IsDirty);
        document.Redo();
        Assert.Equal(2, document.Session.Transform.Zoom);
        Assert.True(document.IsDirty);
        document.MarkSaved("other.cardryft");
        Assert.Equal("other.cardryft", document.ProjectPath);
        Assert.False(document.IsDirty);
        document.New();
        Assert.Null(document.Session);
        Assert.Null(document.ProjectPath);
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void ResetIsUndoable_NewEditInvalidatesRedo_IdenticalEditsDoNotAddHistory()
    {
        var document = new EditorDocument();
        document.Import(new ArtworkSession("source.png"));
        document.Apply(ArtworkTransform.Default);
        Assert.False(document.CanUndo);
        var edited = new ArtworkTransform(3, 1, -1);
        document.Apply(edited);
        document.Apply(ArtworkTransform.Default);
        document.Undo();
        Assert.Equal(edited, document.Session!.Transform);
        Assert.True(document.CanRedo);
        document.Apply(new ArtworkTransform(2, 0, 0));
        Assert.False(document.CanRedo);
        document.Import(new ArtworkSession("replacement.png"));
        Assert.False(document.CanUndo);
        Assert.False(document.CanRedo);
    }

    [Fact]
    public void HistoryIsBounded_AndOpenClearsHistory()
    {
        var document = new EditorDocument();
        document.Import(new ArtworkSession("source.png"));
        for (var i = 0; i < 150; i++) document.Apply(new ArtworkTransform(1 + (i + 1) / 100d, 0, 0));
        var count = 0;
        while (document.CanUndo) { document.Undo(); count++; }
        Assert.Equal(EditorDocument.HistoryLimit, count);
        Assert.Equal(1.5, document.Session!.Transform.Zoom);
        count = 0;
        while (document.CanRedo) { document.Redo(); count++; }
        Assert.Equal(EditorDocument.HistoryLimit, count);
        document.Open(new ArtworkSession("opened.png"), "opened.cardryft");
        Assert.False(document.IsDirty);
        Assert.False(document.CanUndo);
        Assert.False(document.CanRedo);
    }
}
