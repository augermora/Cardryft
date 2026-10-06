namespace Cardryft.Storage;

internal static class LocalPaths
{
    public static string Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\", StringComparison.Ordinal) ||
            new DriveInfo(Path.GetPathRoot(full)!).DriveType == DriveType.Network)
            throw new NotSupportedException("Only local files are supported.");
        return full;
    }

    public static void Write(string path, byte[] bytes)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".cardryft-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static byte[] Read(string path, int limit)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 || stream.Length > limit) throw new InvalidDataException("File size exceeds the supported limit.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
