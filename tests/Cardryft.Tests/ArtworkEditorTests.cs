using Cardryft.App;
using Cardryft.Core;
using Cardryft.Imaging;
using Cardryft.Storage;

namespace Cardryft.Tests;

public sealed class ArtworkEditorTests
{
    [Fact]
    public async Task RecentStorageFailureDoesNotPreventSaveOrOpen()
    {
        using var files = new ImageTestFiles();
        var blocked = files.PathFor("blocked");
        File.WriteAllText(blocked, "not a directory");
        using var editor = new ArtworkEditor(new ImageLoader(), new ArtworkRenderer(), new ProjectStore(),
            new RecentProjects(ApplicationDataPaths.RecentProjectsFile(blocked)));
        try
        {
            await editor.ImportAsync(files.CreateImage("source.png"));
            var project = files.PathFor("project.cardryft");
            editor.Save(project);
            Assert.False(editor.Document.IsDirty);
            Assert.Equal(project, editor.Document.ProjectPath);
            Assert.Contains("recent list", editor.Warning!);
            await editor.NewAsync();
            await editor.OpenAsync(project);
            Assert.True(editor.HasSource);
            Assert.False(editor.Document.IsDirty);
            Assert.Contains("recent list", editor.Warning!);
        }
        finally { await editor.ShutdownAsync(); }
    }

    [Fact]
    public async Task FailedSaveKeepsDirtyStateAndProjectIdentity()
    {
        using var files = new ImageTestFiles();
        using var editor = new ArtworkEditor(new ImageLoader(), new ArtworkRenderer(), new ProjectStore(), new RecentProjects(files.PathFor("recent.json")));
        try
        {
            await editor.ImportAsync(files.CreateImage("source.png"));
            var original = files.PathFor("original.cardryft");
            editor.Save(original);
            editor.SetTransform(new ArtworkTransform(2, 0.2, 0.3));
            var lockedPath = files.PathFor("locked.cardryft");
            File.WriteAllText(lockedPath, "existing");
            using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var error = Record.Exception(() => editor.Save(lockedPath));
                Assert.True(error is IOException or UnauthorizedAccessException);
            }
            Assert.True(editor.Document.IsDirty);
            Assert.Equal(original, editor.Document.ProjectPath);
            Assert.Equal("existing", File.ReadAllText(lockedPath));
        }
        finally { await editor.ShutdownAsync(); }
    }

    [Fact]
    public async Task MissingImagePreservesProject_AndReplacementRetainsTransform()
    {
        using var files = new ImageTestFiles();
        var path = files.PathFor("missing.cardryft");
        var session = new ArtworkSession(files.PathFor("missing.png"), transform: new ArtworkTransform(2, 0.25, -0.5));
        var store = new ProjectStore();
        store.Save(path, session);
        using var editor = new ArtworkEditor(new ImageLoader(), new ArtworkRenderer(), store, new RecentProjects(files.PathFor("recent.json")));
        try
        {
            await editor.OpenAsync(path);
            Assert.Equal(session, editor.Session);
            Assert.False(editor.HasSource);
            Assert.False(editor.Document.IsDirty);
            Assert.NotNull(editor.Warning);
            await editor.ImportAsync(files.CreateImage("replacement.png"));
            Assert.True(editor.HasSource);
            Assert.Equal(session.Transform, editor.Session!.Transform);
            Assert.True(editor.Document.IsDirty);
            Assert.False(editor.Document.CanUndo);
            editor.Save(path);
            Assert.False(editor.Document.IsDirty);
            Assert.Equal(editor.Session, store.Load(path));
            var exportPath = files.PathFor("export.png");
            await editor.ExportAsync(exportPath);
            using var exported = new System.Drawing.Bitmap(exportPath);
            Assert.Equal(1024, exported.Width);
            Assert.Equal(640, exported.Height);
            Assert.False(editor.Document.IsDirty);
        }
        finally { await editor.ShutdownAsync(); }
    }

    [Fact]
    public async Task FailedImportsAndProjectsPreserveCurrentState()
    {
        using var files = new ImageTestFiles();
        using var editor = new ArtworkEditor(new ImageLoader(), new ArtworkRenderer(), new ProjectStore(), new RecentProjects(files.PathFor("recent.json")));
        try
        {
            await editor.ImportAsync(files.CreateImage("source.png"));
            var session = editor.Session;
            var invalidImage = files.PathFor("bad.png");
            var invalidProject = files.PathFor("bad.cardryft");
            File.WriteAllText(invalidImage, "invalid image");
            File.WriteAllText(invalidProject, "invalid project");
            await Assert.ThrowsAsync<InvalidDataException>(() => editor.ImportAsync(invalidImage));
            Assert.Equal(session, editor.Session);
            Assert.True(editor.HasSource);
            await Assert.ThrowsAsync<InvalidDataException>(() => editor.OpenAsync(invalidProject));
            Assert.Equal(session, editor.Session);
            Assert.True(editor.HasSource);
        }
        finally { await editor.ShutdownAsync(); }
    }

    [Theory]
    [InlineData("image.png")] [InlineData("image.jpg")] [InlineData("image.JPEG")]
    public void DropAcceptsSupportedSingleFile(string path) => Assert.Equal(path, ImageDrop.Validate([path]));

    [Fact]
    public void DropRejectsEmptyMultipleOrUnsupportedFiles()
    {
        Assert.Throws<InvalidDataException>(() => ImageDrop.Validate([]));
        Assert.Throws<InvalidDataException>(() => ImageDrop.Validate(["one.png", "two.png"]));
        Assert.Throws<InvalidDataException>(() => ImageDrop.Validate(["program.exe"]));
    }
}
