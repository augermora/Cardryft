namespace Cardryft.Storage;

/// <summary>Per-user application state paths; an explicit root keeps tests isolated.</summary>
public static class ApplicationDataPaths
{
    public static string RecentProjectsFile(string? localApplicationData = null)
    {
        var root = localApplicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new IOException("The local application data directory is unavailable.");
        return LocalPaths.Resolve(Path.Combine(root, "Cardryft", "recent-projects.json"));
    }
}
