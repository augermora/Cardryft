namespace Cardryft.App;

internal static class ImageDrop
{
    public static string Validate(string[] paths)
    {
        if (paths.Length != 1 || Path.GetExtension(paths[0]).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg"))
            throw new InvalidDataException("Drop one local PNG or JPEG image.");
        return paths[0]; // The regular ImageLoader performs all content, local-path and size checks.
    }
}
