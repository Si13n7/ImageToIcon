using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageToIcon.Models;

public class IconThumb(int size, Image<Rgba32> source)
{
    public int Size { get; } = size;
    public Image<Rgba32> Source { get; private set; } = source;
    public Bitmap Preview { get; private set; } = ToAvaloniaBitmap(source);

    public string SizeLabel => Size.ToString();
    public string TooltipText => $"{Size}x{Size}";
    public int DisplaySize => Size;

    public void Replace(Image<Rgba32> newSource)
    {
        Source.Dispose();
        Preview.Dispose();
        Source = newSource;
        Preview = ToAvaloniaBitmap(newSource);
    }

    private static Bitmap ToAvaloniaBitmap(Image<Rgba32> img)
    {
        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder
        {
            ColorType = PngColorType.RgbWithAlpha,
            BitDepth = PngBitDepth.Bit8
        });
        ms.Position = 0;
        return new Bitmap(ms);
    }
}
