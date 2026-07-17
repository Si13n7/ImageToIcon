using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageToIcon.Services;

public static class ImageLoader
{
    public static readonly string[] SupportedExtensions =
    [
        ".bmp", ".dib", ".rle",
        ".jpg", ".jpeg", ".jpe", ".jfif",
        ".tif", ".tiff",
        ".png",
        ".webp",
        ".gif",
        ".tga",
        ".pbm", ".pgm", ".ppm", ".pnm",
        ".qoi",
        ".pbm", ".pgm", ".ppm", ".pnm",
        ".qoi"
    ];

    public static bool IsSupported(string path)
    {
        return SupportedExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    }

    public static Image<Rgba32>? TryLoad(string path)
    {
        try
        {
            return File.Exists(path) ? Image.Load<Rgba32>(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
