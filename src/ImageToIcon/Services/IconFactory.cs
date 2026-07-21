using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageToIcon.Services;

/// Cross-platform ICO writer. Ported from SilDev.Drawing.IconFactory, converted to ImageSharp.
public static class IconFactory
{
    public const int MaxSize = 4096;
    public const int MinSize = 2;

    private const int SizeIconDir = 6;
    private const int SizeIconDirEntry = 16;

    /// Sizes checked by default (Windows 11 application icon set).
    public static readonly IReadOnlyList<int> DefaultSizes =
    [
        256, 48, 32, 24, 16
    ];

    public static IEnumerable<Image<Rgba32>> BuildSizes(Image<Rgba32> source, IEnumerable<int> sizes)
    {
        var maxDim = Math.Max(source.Width, source.Height);
        foreach (var s in sizes.OrderByDescending(x => x))
        {
            if (s > maxDim || s < MinSize || s > MaxSize) continue;
            var clone = source.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(s, s),
                Mode = ResizeMode.Pad,
                Sampler = KnownResamplers.Lanczos3
            }));
            yield return clone;
        }
    }

    public static void Save(IEnumerable<Image<Rgba32>> images, string path)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        Save(images, fs);
    }

    public static void Save(IEnumerable<Image<Rgba32>> images, Stream stream)
    {
        var list = images
                   .Where(i => i is { Width: >= MinSize and <= MaxSize, Height: >= MinSize and <= MaxSize })
                   .OrderByDescending(i => i.Width)
                   .ThenByDescending(i => i.Height)
                   .ToArray();

        if (list.Length == 0)
            throw new InvalidOperationException("No valid images to save.");

        // Encode all frames to PNG buffers first
        var pngEncoder = new PngEncoder { ColorType = PngColorType.RgbWithAlpha };
        var buffers = new byte[list.Length][];
        for (var i = 0; i < list.Length; i++)
        {
            using var ms = new MemoryStream();
            list[i].Save(ms, pngEncoder);
            buffers[i] = ms.ToArray();
        }

        using var bw = new BinaryWriter(stream, Encoding.UTF8, true);

        // ICONDIR
        bw.Write((ushort)0); // reserved
        bw.Write((ushort)1); // type = icon
        bw.Write((ushort)list.Length);

        var offset = (uint)(SizeIconDir + SizeIconDirEntry * list.Length);

        // ICONDIRENTRY records
        for (var i = 0; i < list.Length; i++)
        {
            var img = list[i];
            bw.Write(GetByteDim(img.Width));   // width
            bw.Write(GetByteDim(img.Height));  // height
            bw.Write((byte)0);                 // color count (0 = >= 256)
            bw.Write((byte)0);                 // reserved
            bw.Write((ushort)1);               // color planes
            bw.Write((ushort)32);              // bits per pixel
            bw.Write((uint)buffers[i].Length); // data size
            bw.Write(offset);                  // offset
            offset += (uint)buffers[i].Length;
        }

        // Actual image data
        for (var i = 0; i < list.Length; i++)
            bw.Write(buffers[i]);
    }

    // ICONDIRENTRY width/height is a single byte; 0 means "256 or larger".
    // The actual dimension of >= 256 frames is stored in the embedded PNG's IHDR.
    private static byte GetByteDim(int v)
    {
        return v >= 256 ? (byte)0 : (byte)v;
    }
}