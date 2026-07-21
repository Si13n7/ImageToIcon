using System.ComponentModel;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageToIcon.Models;

public class IconThumb(int size) : INotifyPropertyChanged
{
    public int Size { get; } = size;
    public Image<Rgba32>? Source { get; private set; }
    public Bitmap? Preview { get; private set; }

    public string SizeLabel => Size.ToString();
    public string TooltipText => $"{Size}x{Size}";
    public int DisplaySize => Size;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Fill(Image<Rgba32> newSource, Bitmap newPreview)
    {
        Source?.Dispose();
        Preview?.Dispose();
        Source = newSource;
        Preview = newPreview;
        Raise(nameof(Source));
        Raise(nameof(Preview));
    }

    public void Replace(Image<Rgba32> newSource)
    {
        Fill(newSource, ToAvaloniaBitmap(newSource));
    }

    public void DisposeContent()
    {
        Source?.Dispose();
        Preview?.Dispose();
        Source = null;
        Preview = null;
    }

    public static Bitmap ToAvaloniaBitmap(Image<Rgba32> img)
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

    private void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}