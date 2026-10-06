using Cardryft.Core;
using Cardryft.Storage;

namespace Cardryft.Tests;

public sealed class ProjectStoreTests
{
    [Fact]
    public void ProjectRoundTrip_PreservesState_WithoutRequiringSourceToExist()
    {
        using var files = new ImageTestFiles();
        var store = new ProjectStore();
        var session = new ArtworkSession(files.PathFor("missing-source.png"), transform: new ArtworkTransform(2.25, 0.51, -0.48));
        var path = files.PathFor("project.cardryft");
        store.Save(path, session);
        Assert.Equal(session, store.Load(path));
        var text = File.ReadAllText(path);
        Assert.Contains("\"version\": 1", text);
        Assert.DoesNotContain("pixels", text);
    }

    [Theory]
    [InlineData("{}")] [InlineData("null")] [InlineData("not json")]
    [InlineData("{\"version\":2}")]
    public void MalformedOrIncompleteProjectsAreRejected(string text)
    {
        using var files = new ImageTestFiles();
        var path = files.PathFor("bad.cardryft");
        File.WriteAllText(path, text);
        Assert.Throws<InvalidDataException>(() => new ProjectStore().Load(path));
    }

    [Theory]
    [InlineData("\"version\": 1", "\"version\": 99")]
    [InlineData("\"zoom\": 1", "\"zoom\": 0")]
    [InlineData("\"horizontalOffset\": 0", "\"horizontalOffset\": 2")]
    [InlineData("\"outputWidth\": 1024", "\"outputWidth\": 640")]
    [InlineData("\"version\": 1", "\"extra\": 42, \"version\": 1")]
    public void UnsupportedVersionAndInvalidValuesAreRejected(string before, string after)
    {
        using var files = new ImageTestFiles();
        var path = files.PathFor("invalid.cardryft");
        var store = new ProjectStore();
        store.Save(path, new ArtworkSession(files.PathFor("source.png")));
        File.WriteAllText(path, File.ReadAllText(path).Replace(before, after));
        Assert.Throws<InvalidDataException>(() => store.Load(path));
    }

    [Fact]
    public void OversizedProjectsAndExecutableReferencesAreRejected()
    {
        using var files = new ImageTestFiles();
        var path = files.PathFor("oversized.cardryft");
        File.WriteAllText(path, new string(' ', ProjectStore.MaximumFileBytes + 1));
        var store = new ProjectStore();
        Assert.Throws<InvalidDataException>(() => store.Load(path));
        Assert.Throws<InvalidDataException>(() => store.Save(path, new ArtworkSession(files.PathFor("source.exe"))));
        Assert.Throws<NotSupportedException>(() => store.Save(path, new ArtworkSession(@"\\server\share\source.png")));
        Assert.Throws<InvalidDataException>(() => store.Save(files.PathFor("wrong.json"), new ArtworkSession(files.PathFor("source.png"))));
    }

    [Fact]
    public void FailedSavePreservesExistingFileAndCleansTemporaryFile()
    {
        using var files = new ImageTestFiles();
        var path = files.PathFor("locked.cardryft");
        File.WriteAllText(path, "existing");
        using (var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = Record.Exception(() => new ProjectStore().Save(path, new ArtworkSession(files.PathFor("source.png"))));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("existing", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(files.DirectoryPath, ".cardryft-*.tmp"));
    }

    [Fact]
    public void RecentProjects_AreDeduplicatedTrimmedAndMissingFilesIgnored()
    {
        using var files = new ImageTestFiles();
        var recent = new RecentProjects(files.PathFor("recent.json"));
        for (var i = 0; i < 12; i++)
        {
            var path = files.PathFor($"{i}.cardryft");
            File.WriteAllText(path, "placeholder");
            recent.Add(path);
        }
        Assert.Equal(8, recent.Read().Count);
        Assert.Equal(files.PathFor("11.cardryft"), recent.Read()[0]);
        recent.Add(files.PathFor("7.cardryft"));
        Assert.Equal(8, recent.Read().Count);
        Assert.Equal(files.PathFor("7.cardryft"), recent.Read()[0]);
        File.Delete(files.PathFor("7.cardryft"));
        Assert.Equal(7, recent.Read().Count);
        File.WriteAllText(files.PathFor("recent.json"), "invalid");
        Assert.Empty(recent.Read());
    }
}
