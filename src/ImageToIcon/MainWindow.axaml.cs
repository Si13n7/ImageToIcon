using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ImageToIcon.Models;
using ImageToIcon.Services;
using ImageToIcon.Ui;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;

namespace ImageToIcon;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<SizeToggle> _sizeToggles = [];
    private readonly ObservableCollection<IconThumb> _thumbs = [];
    private Image<Rgba32>? _sourceImage;
    private string? _sourceName;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(string? startupFile)
    {
        InitializeComponent();

        WindowAutoRecenter.Attach(this);

        var settings = Settings.Load();

        var openBtn = this.FindControl<Button>("OpenBtn")!;
        var saveBtn = this.FindControl<Button>("SaveBtn")!;
        var panel = this.FindControl<ItemsControl>("ImgPanel")!;
        var toggles = this.FindControl<ItemsControl>("SizeToggles")!;

        panel.ItemsSource = _thumbs;
        toggles.ItemsSource = _sizeToggles;

        var selected = new HashSet<int>(settings.SelectedSizes);
        foreach (var size in IconFactory.AllSizes)
        {
            var toggle = new SizeToggle(size, selected.Contains(size));
            toggle.PropertyChanged += Toggle_PropertyChanged;
            _sizeToggles.Add(toggle);
        }

        openBtn.Click += async (_, _) => await OpenImageAsync();
        saveBtn.Click += async (_, _) => await SaveIconAsync();

        AddHandler(DragDrop.DropEvent, OnWindowDrop);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver);

        Closing += (_, _) =>
        {
            settings.SelectedSizes = _sizeToggles.Where(t => t.IsChecked).Select(t => t.Size).ToArray();
            settings.Save();
            _sourceImage?.Dispose();
        };

        if (startupFile != null)
        {
            LoadFromPath(startupFile);
        }
        else
        {
            LoadDefaultSymbol();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LoadDefaultSymbol()
    {
        try
        {
            var uri = new Uri("avares://ImageToIcon/Assets/Symbol.png");
            using var stream = AssetLoader.Open(uri);
            _sourceImage = Image.Load<Rgba32>(stream);
            _sourceName = "icon";
            UpdateAvailability();
            RebuildThumbs();
        }
        catch
        {
            // ignored
        }
    }

    private void LoadFromPath(string path)
    {
        var img = ImageLoader.TryLoad(path);
        if (img == null) return;
        _sourceImage?.Dispose();
        _sourceImage = img;
        _sourceName = Path.GetFileNameWithoutExtension(path);
        UpdateAvailability();
        RebuildThumbs();
    }

    private void UpdateAvailability()
    {
        if (_sourceImage == null) return;
        var maxDim = Math.Max(_sourceImage.Width, _sourceImage.Height);
        foreach (var t in _sizeToggles)
            t.IsAvailable = t.Size <= maxDim;
    }

    private void Toggle_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SizeToggle.IsChecked))
            RebuildThumbs();
    }

    private void RebuildThumbs()
    {
        foreach (var t in _thumbs)
        {
            t.Source.Dispose();
            t.Preview.Dispose();
        }

        _thumbs.Clear();
        if (_sourceImage == null) return;
        var maxDim = Math.Max(_sourceImage.Width, _sourceImage.Height);
        var sizes = _sizeToggles
                    .Where(t => t is { IsChecked: true, IsAvailable: true })
                    .Select(t => t.Size)
                    .OrderByDescending(s => s);
        foreach (var size in sizes)
        {
            if (size > maxDim) continue;
            var resized = _sourceImage.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            }));
            _thumbs.Add(new IconThumb(size, resized));
        }
    }

    private async Task OpenImageAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image files")
                {
                    Patterns = ImageLoader.SupportedExtensions.Select(e => "*" + e).ToArray()
                },
                FilePickerFileTypes.All
            ]
        });
        var file = files.FirstOrDefault();
        if (file == null)
            return;
        LoadFromPath(file.Path.LocalPath);
    }

    private async Task SaveIconAsync()
    {
        if (_thumbs.Count == 0) return;
        var suggested = (_sourceName ?? "icon") + ".ico";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save icon",
            SuggestedFileName = suggested,
            DefaultExtension = "ico",
            FileTypeChoices =
            [
                new FilePickerFileType("Icon file") { Patterns = ["*.ico"] }
            ]
        });
        if (file == null) return;
        var images = _thumbs.Select(t => t.Source.Clone(_ => { }));
        try
        {
            IconFactory.Save(images, file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            var dlg = new Window
            {
                Title = "Error",
                Width = 400, Height = 120,
                Content = new TextBlock { Text = ex.Message, Margin = new Thickness(12) }
            };
            await dlg.ShowDialog(this);
        }
    }

    private static void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnWindowDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        var path = files?.FirstOrDefault()?.Path.LocalPath;
        if (path == null || !ImageLoader.IsSupported(path)) return;

        // If dropped on a specific thumb, replace only that one
        if (e.Source is Control c && FindThumb(c) is { } thumb)
        {
            var newImg = ImageLoader.TryLoad(path);
            if (newImg == null) return;
            var resized = newImg.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(thumb.Size, thumb.Size),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            }));
            newImg.Dispose();
            var idx = _thumbs.IndexOf(thumb);
            thumb.Replace(resized);
            _thumbs[idx] = thumb; // trigger refresh
            _thumbs.RemoveAt(idx);
            _thumbs.Insert(idx, thumb);
            return;
        }

        LoadFromPath(path);
    }

    private static IconThumb? FindThumb(Control? c)
    {
        while (c != null)
        {
            if (c.Tag is IconThumb t) return t;
            c = c.Parent as Control;
        }

        return null;
    }

    private async void Thumb_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (sender is not Control { Tag: IconThumb thumb } c) return;
            if (!e.GetCurrentPoint(c).Properties.IsLeftButtonPressed) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Image files")
                    {
                        Patterns = ImageLoader.SupportedExtensions.Select(ext => "*" + ext).ToArray()
                    }
                ]
            });
            var file = files.FirstOrDefault();
            if (file == null)
                return;
            var img = ImageLoader.TryLoad(file.Path.LocalPath);
            if (img == null)
                return;
            var resized = img.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(thumb.Size, thumb.Size),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3
            }));
            img.Dispose();
            var idx = _thumbs.IndexOf(thumb);
            thumb.Replace(resized);
            _thumbs.RemoveAt(idx);
            _thumbs.Insert(idx, thumb);
        }
        catch
        {
            // ignored
        }
    }
}
