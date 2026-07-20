using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ImageToIcon.Platform;
using ImageToIcon.Services;

namespace ImageToIcon.Ui;

public static class SizeInputDialog
{
    // Suggestions for the Add-size dialog. Every entry is base x DPI for
    // bases 16 / 24 / 32 / 48 in 25% steps (125%–500%); GetStatus below
    // annotates each concrete size at runtime.
    //
    // Ordered so PickDefault proposes the frames Windows is most likely
    // to pull: standard DPI (125–200%) first, then 225–300%, then rarer.
    //
    // 256-base scaling is omitted on purpose — Windows never scales the
    // 256 frame; it always picks the largest available regardless of DPI,
    // so suggesting 320+ would only produce misleading oversized frames.
    // Instead, a handful of common high-res replacements for the 256 top
    // frame are offered at the very end (512 / 768 / 1024 / 2048).
    private static readonly int[] SuggestedDefaults =
    [
        // DPI 125-200% on 16/24/32/48
        20, 28, 30, 36, 40, 42,
        56, 60, 64, 72, 84, 96,

        // DPI 225-300%
        54, 80, 108, 120, 144,

        // DPI 275% and 325-500%
        44, 52, 66, 68, 76, 78,
        88, 90, 102, 104, 112, 114,
        128, 132, 136, 152, 156, 160,
        168, 180, 192, 204, 216, 228,
        240,

        // High-res replacements for the 256 top frame
        512, 768, 1024, 2048
    ];

    public static async Task<int?> ShowAsync(Window owner, int? initial, HashSet<int> reserved)
    {
        const int min = IconFactory.MinSize;
        const int max = IconFactory.MaxSize;

        var input = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = initial ?? PickDefault(reserved),
            Increment = 1,
            FormatString = "0",
            Width = 160,
            AllowSpin = true,
            ShowButtonSpinner = true
        };

        var errorText = new TextBlock
        {
            Foreground = Brushes.OrangeRed,
            IsVisible = false,
            FontSize = 11
        };

        var statusText = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsVisible = false
        };

        input.ValueChanged += (_, _) => UpdateStatus(input.Value, statusText);
        UpdateStatus(input.Value, statusText);

        var okBtn = new Button
        {
            Content = "OK",
            Width = 80,
            IsDefault = true,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        var cancelBtn = new Button
        {
            Content = "Cancel",
            Width = 80,
            IsCancel = true,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        int? result = null;
        var dlg = new Window
        {
            Title = initial == null ? "Add Size" : "Edit Size",
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = TryBrush(owner, "AppWindowBrush"),
            Foreground = TryBrush(owner, "AppTextBrush"),
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = $"Icon size in pixels ({min}\u2013{max}):" },
                    input,
                    statusText,
                    errorText,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(0, 8, 0, 0),
                        Children = { okBtn, cancelBtn }
                    }
                }
            }
        };

        okBtn.Click += (_, _) =>
        {
            var raw = input.Value ?? 0m;
            var v = (int)Math.Round(raw);
            if (v is < min or > max)
            {
                errorText.Text = $"Value must be between {min} and {max}.";
                errorText.IsVisible = true;
                return;
            }

            if (reserved.Contains(v) && v != initial)
            {
                errorText.Text = "This size already exists.";
                errorText.IsVisible = true;
                return;
            }

            result = v;
            dlg.Close();
        };
        cancelBtn.Click += (_, _) => dlg.Close();

        dlg.Opened += (_, _) => Win32Window.ApplyDarkTitlebar(dlg);
        await dlg.ShowDialog(owner);
        return result;
    }

    private static IBrush? TryBrush(Window owner, string key)
    {
        return owner.TryFindResource(key, owner.ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : null;
    }

    private static void UpdateStatus(decimal? value, TextBlock target)
    {
        if (value == null)
        {
            target.IsVisible = false;
            return;
        }

        var size = (int)Math.Round(value.Value);
        var info = GetStatus(size);
        if (info == null)
        {
            target.IsVisible = false;
            return;
        }

        target.Text = info.Value.message;
        target.Foreground = info.Value.warning ? Brushes.OrangeRed : Brushes.Goldenrod;
        target.IsVisible = true;
    }

    private static (string message, bool warning)? GetStatus(int size)
    {
        // Windows shell asks for base sizes 16/32/48 at scaling factors
        // in 25% steps. A size can match several base/scale combinations,
        // report every applicable one so users see the full picture.
        var matches = new List<string>();
        foreach (var baseSize in new[] { 48, 32, 24, 16 })
        {
            if (size * 100 % baseSize != 0)
                continue;

            var scale = size * 100 / baseSize;
            if (scale is >= 125 and <= 500 && scale % 25 == 0)
                matches.Add($"Used at {scale}% scaling for {baseSize}px.");
        }

        if (matches.Count > 0)
            return (string.Join('\n', matches), false);

        if (IconFactory.DefaultSizes.Contains(size))
            return null;

        if (size <= 256)
            return ("Not read by any known OS.", true);

        var msg = "Used instead of 256 as top frame.\n\nWindows always picks the\nlargest available.";
        if (size > 1024)
            msg += "\n\nAre you shipping an icon\nor a poster?";
        return (msg, size > 1024);
    }

    private static int PickDefault(HashSet<int> reserved)
    {
        foreach (var s in SuggestedDefaults)
        {
            if (!reserved.Contains(s))
                return s;
        }

        var idx = Random.Shared.Next(SuggestedDefaults.Length);
        return SuggestedDefaults[idx];
    }
}
