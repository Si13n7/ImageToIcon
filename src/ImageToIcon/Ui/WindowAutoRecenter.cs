using Avalonia;
using Avalonia.Controls;

namespace ImageToIcon.Ui;

public static class WindowAutoRecenter
{
    public static void Attach(Window window)
    {
        var last = default(Size);
        var armed = false;

        window.Opened += (_, _) =>
        {
            last = window.ClientSize;
            armed = true;
        };

        window.PropertyChanged += (_, e) =>
        {
            if (!armed || e.Property != TopLevel.ClientSizeProperty)
                return;

            var current = window.ClientSize;
            var dx = (int)((current.Width - last.Width) / 2);
            var dy = (int)((current.Height - last.Height) / 2);
            last = current;

            if (dx == 0 && dy == 0)
                return;

            window.Position = new PixelPoint(window.Position.X - dx, window.Position.Y - dy);
        };
    }
}
