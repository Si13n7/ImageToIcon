using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;
using Svg.Skia;

namespace ImageToIcon.Services;

public static class SvgLoader
{
    public static readonly string[] Extensions = [".svg", ".svgz"];

    public static bool IsSupported(string path)
    {
        return Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    }

    public static Image<Rgba32>? TryLoad(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            return TryLoad(fs);
        }
        catch
        {
            return null;
        }
    }

    public static Image<Rgba32>? TryLoad(Stream stream)
    {
        try
        {
            using var svg = new SKSvg();
            var pic = svg.Load(stream);
            if (pic == null)
                return null;

            var bounds = pic.CullRect;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            const int target = IconFactory.MaxSize;
            var scale = target / Math.Max(bounds.Width, bounds.Height);
            var w = Math.Max(1, (int)Math.Round(bounds.Width * scale));
            var h = Math.Max(1, (int)Math.Round(bounds.Height * scale));

            var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.DrawPicture(pic);
            canvas.Flush();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream();
            data.SaveTo(ms);
            ms.Position = 0;
            return Image.Load<Rgba32>(ms);
        }
        catch
        {
            return null;
        }
    }
}