using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

namespace Cardryft.Tests;

internal static class ImageTestInitialization
{
    [ModuleInitializer]
    internal static void InitializeEncoders()
    {
        // .NET 10's ImageCodecInfoHelper publishes its shared encoder array before filling it.
        // Complete first-use initialization before xUnit can run concurrent fixtures or exports.
        // Keep parallel tests enabled; this affects only the test assembly, not application startup.
        using var bitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        foreach (var format in new[] { ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Gif })
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, format);
        }
    }
}
