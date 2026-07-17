using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ImageToIcon.Services;

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
            var startupFile =
                desktop.Args?.FirstOrDefault(a => !a.StartsWith('-')
                                                  && !a.StartsWith('/')
                                                  && ImageLoader.IsSupported(a)
                                                  && File.Exists(a));

            desktop.MainWindow = new MainWindow(startupFile);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
