using Avalonia;
using ImageToIcon.Services;

namespace ImageToIcon;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(a => a is "--cli" or "/cli" or "--help" or "/?"))
            return RunCli(args);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
                         .UsePlatformDetect()
                         .WithInterFont()
                         .LogToTrace();
    }

    private static int RunCli(string[] args)
    {
        if (args.Any(a => a is "--help" or "/?"))
        {
            PrintHelp();
            return 0;
        }

        var outArg = args.FirstOrDefault(a => a.StartsWith("--o=") || a.StartsWith("/o="));
        if (string.IsNullOrWhiteSpace(outArg))
        {
            Console.WriteLine("Error: Missing output path.");
            PrintHelp();
            return 1;
        }

        var outDir = outArg[(outArg.IndexOf('=') + 1)..].Trim('"');
        if (!Directory.Exists(outDir))
        {
            Console.WriteLine($"Error: Output path '{outDir}' does not exist.");
            return 1;
        }

        var sizes = ParseSizes(args) ?? IconFactory.DefaultSizes.ToArray();

        var inputs = args.Where(a => !a.StartsWith('-') && !a.StartsWith('/') && ImageLoader.IsSupported(a));
        var any = false;
        foreach (var file in inputs)
        {
            any = true;
            if (!File.Exists(file))
            {
                Console.WriteLine($"Skipped: '{file}' not found.");
                continue;
            }

            Console.WriteLine($"Processing '{file}' ...");
            using var img = ImageLoader.TryLoad(file);
            if (img == null)
            {
                Console.WriteLine("Skipped: Invalid image format.");
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(file);
            var outPath = Path.Combine(outDir, name + ".ico");
            if (File.Exists(outPath))
                outPath = Path.Combine(outDir, $"{name}___{DateTime.Now:yyyyMMddHHmmssfff}.ico");

            var frames = IconFactory.BuildSizes(img, sizes).ToList();
            try
            {
                IconFactory.Save(frames, outPath);
                Console.WriteLine($"Done: '{outPath}' saved.");
            }
            finally
            {
                foreach (var f in frames) f.Dispose();
            }
        }

        if (!any)
        {
            Console.WriteLine("No input images provided.");
            PrintHelp();
            return 1;
        }

        return 0;
    }

    private static int[]? ParseSizes(string[] args)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith("--sizes=") || a.StartsWith("/sizes="));
        if (arg == null) return null;
        var raw = arg[(arg.IndexOf('=') + 1)..].Trim('"');
        if (raw.Equals("all", StringComparison.OrdinalIgnoreCase))
            return IconFactory.AllSizes.ToArray();
        var parsed = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => int.TryParse(s, out var n) ? n : -1)
                        .Where(n => IconFactory.AllSizes.Contains(n))
                        .Distinct()
                        .ToArray();
        return parsed.Length > 0 ? parsed : null;
    }

    private static void PrintHelp()
    {
        var name = Environment.ProcessPath is { } p ? Path.GetFileNameWithoutExtension(p) : "ImageToIcon";
        Console.WriteLine($"""
                           Usage: {name} [--cli] --o=<output-dir> [--sizes=<list>|all] <image1> [image2 ...]

                             --cli, /cli           Run in CLI mode.
                             --o=DIR, /o=DIR       Output directory for .ico files.
                             --sizes=16,32,64      Comma-separated list of icon sizes to generate.
                             --sizes=all           Generate every supported size.
                             --help, /?            Show this help.

                           Default sizes:    {string.Join(", ", IconFactory.DefaultSizes)}
                           Available sizes:  {string.Join(", ", IconFactory.AllSizes)}
                           Supported input:  {string.Join(", ", ImageLoader.SupportedExtensions)}
                           """);
    }
}
