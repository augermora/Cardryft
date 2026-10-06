using Cardryft.Core;

namespace Cardryft.Tests;

public sealed class ArtworkProjectTests
{
    [Theory]
    [InlineData("Travel artwork", "Travel artwork")]
    [InlineData("  Travel artwork  ", "Travel artwork")]
    [InlineData("\tTravel artwork\r\n", "Travel artwork")]
    public void Constructor_PreservesNameAndTrimsSurroundingWhitespace(
        string name, string expected)
    {
        var project = new ArtworkProject(name);

        Assert.Equal(expected, project.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_RejectsBlankName(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ArtworkProject(name));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Constructor_RejectsNullName()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ArtworkProject(null!));

        Assert.Equal("name", exception.ParamName);
    }
}
