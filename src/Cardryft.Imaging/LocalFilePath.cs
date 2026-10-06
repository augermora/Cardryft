namespace Cardryft.Imaging;

internal static class LocalFilePath
{
    public static string Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = System.IO.Path.GetFullPath(path);
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) ||
            new DriveInfo(System.IO.Path.GetPathRoot(fullPath)!).DriveType == DriveType.Network)
        {
            throw new NotSupportedException("Only local files are supported.");
        }

        return fullPath;
    }
}
