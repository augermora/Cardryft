using System.Text.Json;
using System.Text.Json.Serialization;
using Cardryft.Core;

namespace Cardryft.Storage;

public sealed class ProjectStore
{
    public const int FormatVersion = 1;
    public const int MaximumFileBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8,
    };

    public void Save(string path, ArtworkSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var full = ProjectPath(path);
        var source = SourcePath(session.SourcePath);
        var reference = Path.GetRelativePath(Path.GetDirectoryName(full)!, source);
        if (!Path.IsPathRooted(reference)) reference = reference.Replace(Path.DirectorySeparatorChar, '/');
        var file = new ProjectFile(FormatVersion, reference, session.Transform.Zoom,
            session.Transform.HorizontalOffset, session.Transform.VerticalOffset, session.OutputWidth, session.OutputHeight);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, Options);
        if (bytes.Length > MaximumFileBytes) throw new InvalidDataException("Project is too large.");
        LocalPaths.Write(full, bytes);
    }

    public ArtworkSession Load(string path)
    {
        try
        {
            var full = ProjectPath(path);
            var file = JsonSerializer.Deserialize<ProjectFile>(LocalPaths.Read(full, MaximumFileBytes), Options)
                ?? throw new InvalidDataException("The project is empty.");
            if (file.Version != FormatVersion) throw new InvalidDataException("Unsupported Cardryft project version.");
            return new ArtworkSession(ResolveSource(file.SourcePath, full), new ArtworkSize(file.OutputWidth, file.OutputHeight),
                new ArtworkTransform(file.Zoom, file.HorizontalOffset, file.VerticalOffset));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        { throw new InvalidDataException("Invalid Cardryft project data.", exception); }
    }

    public static string ProjectPath(string path)
    {
        var full = LocalPaths.Resolve(path);
        if (!Path.GetExtension(full).Equals(".cardryft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a .cardryft project file.");
        return full;
    }

    private static string SourcePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("The source reference must be an absolute local path.");
        var full = LocalPaths.Resolve(path);
        if (Path.GetExtension(full).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg"))
            throw new InvalidDataException("The source must reference a PNG or JPEG image.");
        return full;
    }

    private static string ResolveSource(string reference, string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (Path.IsPathFullyQualified(reference)) return SourcePath(reference);
        if (Path.IsPathRooted(reference))
            throw new InvalidDataException("Source references must be absolute or relative to the project directory.");
        return SourcePath(Path.GetFullPath(reference, Path.GetDirectoryName(projectPath)!));
    }

    private sealed record ProjectFile(
        [property: JsonRequired] int Version, [property: JsonRequired] string SourcePath,
        [property: JsonRequired] double Zoom, [property: JsonRequired] double HorizontalOffset,
        [property: JsonRequired] double VerticalOffset, [property: JsonRequired] int OutputWidth,
        [property: JsonRequired] int OutputHeight);
}
