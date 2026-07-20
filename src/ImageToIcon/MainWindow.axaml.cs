using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ImageToIcon.Models;
using ImageToIcon.Platform;
using ImageToIcon.Services;
using ImageToIcon.Ui;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Size = SixLabors.ImageSharp.Size;

namespace ImageToIcon;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _cts = new();
    private readonly SelfUpdateCoordinator _selfUpdate;
    private readonly Settings _settings;
    private readonly ObservableCollection<SizeToggle> _sizeToggles = [];
    private readonly ObservableCollection<IconThumb> _thumbs = [];
    private readonly Button _updateBtn;
    private Image<Rgba32>? _sourceImage;
    private string? _sourceName;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(string? startupFile)
    {
        InitializeComponent();

        if (OperatingSystem.IsWindows())
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

        WindowAutoRecenter.Attach(this);

        _settings = Settings.Load();
        _selfUpdate = new SelfUpdateCoordinator(_settings, _cts.Token);

        var openBtn = this.FindControl<Button>("OpenBtn")!;
        var saveBtn = this.FindControl<Button>("SaveBtn")!;
        var addSizeBtn = this.FindControl<Button>("AddSizeBtn")!;
        _updateBtn = this.FindControl<Button>("UpdateBtn")!;
        var panel = this.FindControl<ItemsControl>("ImgPanel")!;
        var toggles = this.FindControl<ItemsControl>("SizeToggles")!;

        panel.ItemsSource = _thumbs;
        toggles.ItemsSource = _sizeToggles;

        var selected = new HashSet<int>(_settings.SelectedSizes);
        var defaults = new HashSet<int>(IconFactory.DefaultSizes);
        var union = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        foreach (var s in IconFactory.DefaultSizes)
            union.Add(s);
        foreach (var s in _settings.CustomSizes)
            union.Add(s);
        foreach (var toggle in union.Select(size => new SizeToggle(size, selected.Contains(size), !defaults.Contains(size))))
        {
            toggle.PropertyChanged += Toggle_PropertyChanged;
            _sizeToggles.Add(toggle);
        }

        openBtn.Click += async (_, _) => await OpenImageAsync();
        saveBtn.Click += async (_, _) => await SaveIconAsync();
        addSizeBtn.Click += async (_, _) => await AddCustomSizeAsync();
        _updateBtn.Click += async (_, _) => await OnUpdateClickAsync();

        // Show cached pending update immediately, then check in background.
        RefreshUpdateButton();
        Opened += (_, _) =>
        {
            Win32Window.ApplyDarkTitlebar(this);
            _ = CheckForUpdateAsync();
        };

        if (Application.Current is { } app)
            app.ActualThemeVariantChanged += (_, _) => Win32Window.ApplyDarkTitlebar(this);

        AddHandler(DragDrop.DropEvent, OnWindowDrop);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver);

        Closing += (_, _) =>
        {
            _settings.SelectedSizes = _sizeToggles.Where(t => t.IsChecked).Select(t => t.Size).ToArray();
            _settings.CustomSizes = _sizeToggles.Where(t => t.IsCustom).Select(t => t.Size).ToArray();
            _settings.Save();
            _cts.Cancel();
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
            var uri = new Uri("avares://ImageToIcon/Assets/Symbol.svg");
            using var stream = AssetLoader.Open(uri);
            _sourceImage = SvgLoader.TryLoad(stream);
            if (_sourceImage == null) return;
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
        if (img == null)
            return;
        _sourceImage?.Dispose();
        _sourceImage = img;
        _sourceName = Path.GetFileNameWithoutExtension(path);
        UpdateAvailability();
        RebuildThumbs();
    }

    private void UpdateAvailability()
    {
        if (_sourceImage == null)
            return;
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
        if (_sourceImage == null)
            return;
        var maxDim = Math.Max(_sourceImage.Width, _sourceImage.Height);
        var sizes = _sizeToggles
                    .Where(t => t is { IsChecked: true, IsAvailable: true })
                    .Select(t => t.Size)
                    .OrderByDescending(s => s);
        foreach (var size in sizes)
        {
            if (size > maxDim)
                continue;
            var resized = _sourceImage.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Pad,
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
            FileTypeFilter = ImageLoader.BuildFilePickerFilters()
        });
        var file = files.FirstOrDefault();
        if (file == null)
            return;
        LoadFromPath(file.Path.LocalPath);
    }

    private async Task SaveIconAsync()
    {
        if (_thumbs.Count == 0)
            return;
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
        if (file == null)
            return;
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
        if (path == null || !ImageLoader.IsSupported(path))
            return;

        // If dropped on a specific thumb, replace only that one
        if (e.Source is Control c && FindThumb(c) is { } thumb)
        {
            var newImg = ImageLoader.TryLoad(path);
            if (newImg == null)
                return;
            var resized = newImg.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(thumb.Size, thumb.Size),
                Mode = ResizeMode.Pad,
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
            if (c.Tag is IconThumb t)
                return t;
            c = c.Parent as Control;
        }

        return null;
    }

    private void InsertSorted(SizeToggle t)
    {
        var i = 0;
        while (i < _sizeToggles.Count && _sizeToggles[i].Size > t.Size)
            i++;
        _sizeToggles.Insert(i, t);
    }

    private void ApplyAvailabilityTo(SizeToggle t)
    {
        if (_sourceImage == null)
            return;
        var maxDim = Math.Max(_sourceImage.Width, _sourceImage.Height);
        t.IsAvailable = t.Size <= maxDim;
    }

    private async Task AddCustomSizeAsync()
    {
        var used = new HashSet<int>(_sizeToggles.Select(t => t.Size));
        var newSize = await SizeInputDialog.ShowAsync(this, null, used);
        if (newSize == null)
            return;
        var toggle = new SizeToggle(newSize.Value, true, true);
        toggle.PropertyChanged += Toggle_PropertyChanged;
        InsertSorted(toggle);
        ApplyAvailabilityTo(toggle);
        RebuildThumbs();
    }

    private async Task EditCustomSizeAsync(SizeToggle t)
    {
        var used = new HashSet<int>(_sizeToggles.Where(x => x != t).Select(x => x.Size));
        var newSize = await SizeInputDialog.ShowAsync(this, t.Size, used);
        if (newSize == null || newSize == t.Size)
            return;
        var wasChecked = t.IsChecked;
        t.PropertyChanged -= Toggle_PropertyChanged;
        _sizeToggles.Remove(t);
        var nt = new SizeToggle(newSize.Value, wasChecked, true);
        nt.PropertyChanged += Toggle_PropertyChanged;
        InsertSorted(nt);
        ApplyAvailabilityTo(nt);
        RebuildThumbs();
    }

    private void RemoveCustomSize(SizeToggle t)
    {
        t.PropertyChanged -= Toggle_PropertyChanged;
        _sizeToggles.Remove(t);
        RebuildThumbs();
    }

    private void Pill_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: SizeToggle { IsCustom: true } t } ctrl)
        {
            e.Handled = true;
            return;
        }

        var menu = new ContextMenu();
        var edit = new MenuItem { Header = "Edit..." };
        edit.Click += async (_, _) => await EditCustomSizeAsync(t);
        var remove = new MenuItem { Header = "Remove" };
        remove.Click += (_, _) => RemoveCustomSize(t);
        menu.Items.Add(edit);
        menu.Items.Add(remove);
        menu.Open(ctrl);
        e.Handled = true;
    }

    private async void Thumb_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (sender is not Control { Tag: IconThumb thumb } c)
                return;
            if (!e.GetCurrentPoint(c).Properties.IsLeftButtonPressed)
                return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = ImageLoader.BuildFilePickerFilters()
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
                Mode = ResizeMode.Pad,
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

    private void RefreshUpdateButton()
    {
        var version = _selfUpdate.Pending?.Version
                      ?? (Version.TryParse(_settings.PendingUpdateVersion, out var v) ? v : null);
        if (version == null)
        {
            _updateBtn.IsVisible = false;
            return;
        }

        _updateBtn.Content = $"Update {version}";
        ToolTip.SetTip(_updateBtn, $"Install ImageToIcon {version}");
        _updateBtn.IsVisible = true;
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            await _selfUpdate.CheckAsync();
        }
        catch
        {
            // best-effort
        }

        RefreshUpdateButton();
    }

    private async Task OnUpdateClickAsync()
    {
        var pending = _selfUpdate.Pending;
        var version = pending?.Version
                      ?? (Version.TryParse(_settings.PendingUpdateVersion, out var v) ? v : null);
        if (version == null)
            return;

        var body = await SelfUpdater.FetchLatestBodyAsync(CancellationToken.None);
        var confirmed = await ChangelogDialog.ShowAsync(this, version, body);
        if (!confirmed)
            return;

        _updateBtn.IsEnabled = false;
        try
        {
            await UpdateProgressDialog.RunAsync(this, _selfUpdate);
        }
        finally
        {
            _updateBtn.IsEnabled = true;
        }
    }
}