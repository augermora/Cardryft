using System.Text.Json;
using Cardryft.Core;
using Cardryft.Storage;

namespace Cardryft.Tests;

public sealed class PersistencePathTests
{
    [Fact]
    public void RecentStorageUsesIsolatedLocalApplicationDataAndCreatesDirectoryOnAdd()
    {
        using var files = new ImageTestFiles();
        var root = files.PathFor("LocalAppData");
        var path = ApplicationDataPaths.RecentProjectsFile(root);
        Assert.Equal(Path.Combine(root, "Cardryft", "recent-projects.json"), path);
        var recent = new RecentProjects(path);
        Assert.Empty(recent.Read());
        Assert.False(Directory.Exists(root));
        var project = files.PathFor("project.cardryft");
        File.WriteAllText(project, "placeholder");
        recent.Add(project);
        Assert.True(File.Exists(path));
        Assert.Equal(new[] { project }, recent.Read());
    }

    [Fact]
    public void UnavailableRecentStorageReturnsEmptyAndReportsWriteFailure()
    {
        using var files = new ImageTestFiles();
        var blocked = files.PathFor("blocked");
        File.WriteAllText(blocked, "a file cannot serve as an application data directory");
        var recent = new RecentProjects(ApplicationDataPaths.RecentProjectsFile(blocked));
        Assert.Empty(recent.Read());
        var project = files.PathFor("project.cardryft");
        File.WriteAllText(project, "placeholder");
        Assert.ThrowsAny<IOException>(() => recent.Add(project));
        Assert.Equal("placeholder", File.ReadAllText(project));
    }

    [Fact]
    public void SaveUsesRelativeImageReferenceAndPreservesAllState()
    {
        using var files = new ImageTestFiles();
        var source = files.CreateImage("source.png");
        var session = new ArtworkSession(source, transform: new ArtworkTransform(2.5, -0.3, 0.7));
        var path = files.PathFor("project.cardryft");
        var store = new ProjectStore();
        store.Save(path, session);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("source.png", json.RootElement.GetProperty("sourcePath").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
        Assert.DoesNotContain(files.DirectoryPath, File.ReadAllText(path));
        Assert.Equal(session, store.Load(path));
    }

    [Fact]
    public void RelativeProjectAndImageCanMoveTogether()
    {
        using var files = new ImageTestFiles();
        var original = files.PathFor("original");
        var moved = files.PathFor("moved");
        Directory.CreateDirectory(Path.Combine(original, "images"));
        var source = Path.Combine(original, "images", "source.jpg");
        File.Copy(files.CreateImage("image.jpg"), source);
        var session = new ArtworkSession(source, transform: new ArtworkTransform(3, 0.4, -0.2));
        var store = new ProjectStore();
        store.Save(Path.Combine(original, "project.cardryft"), session);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(original, "project.cardryft")));
        Assert.Equal("images/source.jpg", json.RootElement.GetProperty("sourcePath").GetString());
        // Move only checked paths under this test's repository-local directory.
        Assert.StartsWith(files.DirectoryPath + Path.DirectorySeparatorChar, Path.GetFullPath(original));
        Assert.StartsWith(files.DirectoryPath + Path.DirectorySeparatorChar, Path.GetFullPath(moved));
        Directory.Move(original, moved);
        var loaded = store.Load(Path.Combine(moved, "project.cardryft"));
        Assert.Equal(Path.Combine(moved, "images", "source.jpg"), loaded.SourcePath);
        Assert.True(File.Exists(loaded.SourcePath));
        Assert.Equal(session.Transform, loaded.Transform);
        Assert.Equal(session.OutputSize, loaded.OutputSize);
    }

    [Fact]
    public void SaveAsComputesParentReferenceAgainstNewProjectDirectory()
    {
        using var files = new ImageTestFiles();
        var session = new ArtworkSession(files.CreateImage("source.png"));
        var directory = files.PathFor("projects");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "copy.cardryft");
        var store = new ProjectStore();
        store.Save(path, session);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("../source.png", json.RootElement.GetProperty("sourcePath").GetString());
        Assert.Equal(session, store.Load(path));
    }

    [Fact]
    public void DifferentDriveFallsBackToAbsoluteReferenceWithoutAccessingTheSource()
    {
        using var files = new ImageTestFiles();
        var usedRoots = DriveInfo.GetDrives().Select(drive => drive.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var otherRoot = Enumerable.Range('D', 23).Select(letter => $"{(char)letter}:\\")
            .First(root => !usedRoots.Contains(root));
        var source = Path.Combine(otherRoot, "Artwork", "source.png");
        var session = new ArtworkSession(source);
        var path = files.PathFor("project.cardryft");
        var store = new ProjectStore();
        store.Save(path, session);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(source, json.RootElement.GetProperty("sourcePath").GetString());
        Assert.Equal(session, store.Load(path));
    }

    [Theory]
    [InlineData("images/missing.png")]
    [InlineData("images\\missing.png")]
    public void MissingRelativeSourceResolvesAndRetainsEditorState(string reference)
    {
        using var files = new ImageTestFiles();
        var path = files.PathFor("missing.cardryft");
        WriteProject(path, reference);
        var session = new ProjectStore().Load(path);
        Assert.Equal(Path.Combine(files.DirectoryPath, "images", "missing.png"), session.SourcePath);
        Assert.False(File.Exists(session.SourcePath));
        Assert.Equal(new ArtworkTransform(2, 0.25, -0.5), session.Transform);
        Assert.Equal(ArtworkSize.Canonical, session.OutputSize);
    }

    [Fact]
    public void ExistingVersionOneAbsoluteReferenceStillLoads()
    {
        using var files = new ImageTestFiles();
        var source = files.PathFor("old-source.jpeg");
        var path = files.PathFor("old.cardryft");
        WriteProject(path, source);
        var loaded = new ProjectStore().Load(path);
        Assert.Equal(new ArtworkSession(source, transform: new ArtworkTransform(2, 0.25, -0.5)), loaded);
    }

    [Theory]
    [InlineData("C:source.png")]
    [InlineData("\\source.png")]
    [InlineData("../source.exe")]
    [InlineData("\\\\server\\share\\source.png")]
    [InlineData("")]
    public void AmbiguousOrUnsupportedSourceReferencesAreRejected(string reference)
    {
        using var files = new ImageTestFiles();
        var path = files.PathFor("bad.cardryft");
        WriteProject(path, reference);
        var error = Record.Exception(() => new ProjectStore().Load(path));
        Assert.True(error is InvalidDataException or NotSupportedException);
    }

    private static void WriteProject(string path, string sourcePath) => File.WriteAllText(path,
        JsonSerializer.Serialize(new { version = 1, sourcePath, zoom = 2, horizontalOffset = 0.25,
            verticalOffset = -0.5, outputWidth = 1024, outputHeight = 640 }));
}
