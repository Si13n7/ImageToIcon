using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ImageToIcon.Services;
using ImageToIcon.Views;

namespace ImageToIcon;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startupFile = desktop.Args?.FirstOrDefault(a => !IsSwitch(a)
                                                                && ImageLoader.IsSupported(a)
                                                                && File.Exists(a));

            desktop.MainWindow = new MainWindow(startupFile);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool IsSwitch(string arg)
    {
        if (arg.Length < 2)
            return false;
        if (arg.StartsWith("--"))
            return true;
        return arg[0] == '/' && arg.IndexOf('/', 1) < 0 && !arg.Contains('\\');
    }
}
