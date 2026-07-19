using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ImageToIcon.Services;

namespace ImageToIcon.Ui;

public static class SizeInputDialog
{
    public static async Task<int?> ShowAsync(Window owner, int? initial, HashSet<int> reserved)
    {
        const int min = IconFactory.MinSize;
        const int max = IconFactory.MaxSize;

        var input = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = initial ?? 64,
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

        var okBtn = new Button { Content = "OK", Width = 80, IsDefault = true };
        var cancelBtn = new Button { Content = "Cancel", Width = 80, IsCancel = true };

        int? result = null;
        var dlg = new Window
        {
            Title = initial == null ? "Add size" : "Edit size",
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = $"Icon size in pixels ({min}\u2013{max}):" },
                    input,
                    errorText,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(0, 8, 0, 0),
                        Children = { cancelBtn, okBtn }
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

        await dlg.ShowDialog(owner);
        return result;
    }
}