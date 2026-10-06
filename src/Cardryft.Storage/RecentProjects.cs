using System.Text.Json;

namespace Cardryft.Storage;

public sealed class RecentProjects(string? path = null)
{
    public const int MaximumEntries = 8;
    private string StoragePath => path is null ? ApplicationDataPaths.RecentProjectsFile() : LocalPaths.Resolve(path);

    public IReadOnlyList<string> Read()
    {
        try
        {
            var full = StoragePath;
            if (!File.Exists(full)) return [];
            var entries = JsonSerializer.Deserialize<string[]>(LocalPaths.Read(full, ProjectStore.MaximumFileBytes)) ?? [];
            return Filter(entries);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or
            UnauthorizedAccessException or ArgumentException or NotSupportedException) { return []; }
    }

    public void Add(string projectPath)
    {
        var project = ProjectStore.ProjectPath(projectPath);
        var entries = Filter(new[] { project }.Concat(Read()));
        var full = StoragePath;
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        LocalPaths.Write(full, JsonSerializer.SerializeToUtf8Bytes(entries));
    }

    private static IReadOnlyList<string> Filter(IEnumerable<string> entries)
    {
        var result = new List<string>();
        foreach (var entry in entries)
        {
            try
            {
                var full = ProjectStore.ProjectPath(entry);
                if (File.Exists(full) && !result.Contains(full, StringComparer.OrdinalIgnoreCase)) result.Add(full);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidDataException or NotSupportedException or IOException) { }
            if (result.Count == MaximumEntries) break;
        }
        return result;
    }
}
