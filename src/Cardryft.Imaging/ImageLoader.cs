using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Cardryft.Imaging;

public sealed class ImageLoader
{
    public const long MaximumFileBytes = 25 * 1024 * 1024;
    public const int MaximumDimension = 8192;
    public const long MaximumPixels = 32_000_000;

    public SourceImage Load(string path)
    {
        var fullPath = LocalFilePath.Resolve(path);
        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg"))
        {
            throw new InvalidDataException("Only PNG and JPEG images are supported.");
        }

        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length == 0 || stream.Length > MaximumFileBytes)
        {
            throw new InvalidDataException("The image must be nonempty and no larger than 25 MiB.");
        }

        // Inspect dimensions before invoking the native decoder to bound raster allocation.
        var dimensions = extension == ".png" ? ReadPngDimensions(stream) : ReadJpegDimensions(stream);
        ValidateDimensions(dimensions.Width, dimensions.Height);
        stream.Position = 0;
        Bitmap? pixels = null;
        try
        {
            using var decoded = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            var expectedFormat = extension == ".png" ? ImageFormat.Png : ImageFormat.Jpeg;
            if (decoded.RawFormat.Guid != expectedFormat.Guid ||
                decoded.Width != dimensions.Width || decoded.Height != dimensions.Height)
            {
                throw new InvalidDataException("The image content does not match its declared format or dimensions.");
            }

            ApplyOrientation(decoded);
            ValidateDimensions(decoded.Width, decoded.Height);
            pixels = new Bitmap(decoded.Width, decoded.Height, PixelFormat.Format32bppArgb);
            pixels.SetResolution(96, 96);
            using (var graphics = Graphics.FromImage(pixels))
            {
                graphics.PageUnit = GraphicsUnit.Pixel;
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.DrawImage(decoded, new Rectangle(0, 0, decoded.Width, decoded.Height),
                    0, 0, decoded.Width, decoded.Height, GraphicsUnit.Pixel);
            }

            var result = new SourceImage(fullPath, pixels);
            pixels = null;
            return result;
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or OutOfMemoryException)
        {
            throw new InvalidDataException("The image could not be decoded as a valid PNG or JPEG.", exception);
        }
        finally
        {
            pixels?.Dispose();
        }
    }

    private static (int Width, int Height) ReadPngDimensions(Stream stream)
    {
        Span<byte> header = stackalloc byte[24];
        if (stream.Read(header) != header.Length ||
            !header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !header[12..16].SequenceEqual("IHDR"u8) || BinaryPrimitives.ReadInt32BigEndian(header[8..12]) != 13)
        {
            throw new InvalidDataException("Invalid PNG header.");
        }

        return (BinaryPrimitives.ReadInt32BigEndian(header[16..20]),
            BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
    }

    private static (int Width, int Height) ReadJpegDimensions(Stream stream)
    {
        if (stream.ReadByte() != 0xff || stream.ReadByte() != 0xd8)
        {
            throw new InvalidDataException("Invalid JPEG header.");
        }

        while (stream.Position < stream.Length)
        {
            if (stream.ReadByte() != 0xff) break;
            int marker;
            do { marker = stream.ReadByte(); } while (marker == 0xff);
            if (marker is < 0 or 0xda or 0xd9) break;
            if (marker is 0x01 or >= 0xd0 and <= 0xd8) continue;
            var length = ReadBigEndianWord(stream);
            if (length < 2 || stream.Position + length - 2 > stream.Length) break;
            if (marker is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7 or >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf)
            {
                if (length < 8) break;
                stream.ReadByte(); // sample precision
                var height = ReadBigEndianWord(stream);
                var width = ReadBigEndianWord(stream);
                return (width, height);
            }

            stream.Position += length - 2;
        }

        throw new InvalidDataException("No valid JPEG dimensions were found.");
    }

    private static int ReadBigEndianWord(Stream stream)
    {
        var high = stream.ReadByte();
        var low = stream.ReadByte();
        if (high < 0 || low < 0) throw new InvalidDataException("Truncated JPEG header.");
        return (high << 8) | low;
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width < 1 || height < 1 || width > MaximumDimension || height > MaximumDimension ||
            (long)width * height > MaximumPixels)
        {
            throw new InvalidDataException("Image dimensions exceed the supported limits.");
        }
    }

    private static void ApplyOrientation(Image image)
    {
        const int orientationId = 0x112;
        if (!image.PropertyIdList.Contains(orientationId)) return;
        var bytes = image.GetPropertyItem(orientationId)?.Value;
        if (bytes is not { Length: >= 2 }) return;
        var orientation = BitConverter.ToUInt16(bytes, 0);
        var flip = orientation switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone,
        };
        image.RotateFlip(flip);
    }
}
