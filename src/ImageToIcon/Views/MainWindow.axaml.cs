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

namespace ImageToIcon.Views;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _cts = new();
    private readonly SelfUpdateCoordinator _selfUpdate;
    private readonly Settings _settings;
    private readonly ObservableCollection<SizeToggle> _sizeToggles = [];
    private readonly ItemsControl? _smallPanel;
    private readonly ObservableCollection<IconThumb> _smallThumbs = [];
    private readonly ObservableCollection<IconThumb> _topThumbs = [];
    private readonly Button _updateBtn;
    private Image<Rgba32>? _sourceImage;
    private string? _sourceName;

    private bool _suppressTopFrameSync;

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
        var topPanel = this.FindControl<ItemsControl>("TopThumbs")!;
        _smallPanel = this.FindControl<ItemsControl>("SmallThumbs")!;
        var toggles = this.FindControl<ItemsControl>("SizeToggles")!;

        topPanel.ItemsSource = _topThumbs;
        _smallPanel.ItemsSource = _smallThumbs;
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

        // Persisted state may have multiple >=256 sizes checked; keep only
        // the largest to match the runtime radio-style rule.
        var topKeep = _sizeToggles
                      .Where(t => t is { Size: >= 256, IsChecked: true })
                      .MaxBy(t => t.Size);
        if (topKeep != null)
            EnforceTopFrameExclusivity(topKeep);

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
        if (e.PropertyName != nameof(SizeToggle.IsChecked))
            return;

        // Windows only ever picks the largest available frame from anything
        // >= 256, so treat that band as radio-style: checking one clears
        // the others, but nothing gets permanently disabled.
        if (sender is SizeToggle { Size: >= 256, IsChecked: true } changed)
            EnforceTopFrameExclusivity(changed);

        RebuildThumbs();
    }

    private void EnforceTopFrameExclusivity(SizeToggle keep)
    {
        if (_suppressTopFrameSync)
            return;
        _suppressTopFrameSync = true;
        try
        {
            foreach (var t in _sizeToggles)
            {
                if (t != keep && t.Size >= 256)
                    t.IsChecked = false;
            }
        }
        finally
        {
            _suppressTopFrameSync = false;
        }
    }

    private void RebuildThumbs()
    {
        foreach (var t in _topThumbs.Concat(_smallThumbs))
        {
            t.Source.Dispose();
            t.Preview.Dispose();
        }

        _topThumbs.Clear();
        _smallThumbs.Clear();
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
            CollectionFor(size).Add(new IconThumb(size, resized));
        }

        // Cap the small-thumb column height at the active top-frame height
        // so WrapPanel breaks into a new column instead of growing forever.
        if (_smallPanel == null)
            return;
        var topSize = _topThumbs.FirstOrDefault()?.Size;
        _smallPanel.MaxHeight = topSize ?? double.PositiveInfinity;
    }

    private ObservableCollection<IconThumb> CollectionFor(int size)
    {
        return size >= 256 ? _topThumbs : _smallThumbs;
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
        if (_topThumbs.Count == 0 && _smallThumbs.Count == 0)
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
        var images = _topThumbs.Concat(_smallThumbs)
                               .OrderByDescending(t => t.Size)
                               .Select(t => t.Source.Clone(_ => { }));
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
            var col = CollectionFor(thumb.Size);
            var idx = col.IndexOf(thumb);
            thumb.Replace(resized);
            col.RemoveAt(idx);
            col.Insert(idx, thumb);
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

    private async Task AddCustomSizeAsync()
    {
        var defaults = new HashSet<int>(IconFactory.DefaultSizes);
        var currentCustom = _sizeToggles.Where(t => t.IsCustom).Select(t => t.Size).ToHashSet();
        var result = await AddSizesDialog.ShowAsync(this, defaults, currentCustom);
        if (result == null)
            return;

        var target = new HashSet<int>(result);
        foreach (var t in _sizeToggles.Where(t => t.IsCustom && !target.Contains(t.Size)).ToList())
        {
            t.PropertyChanged -= Toggle_PropertyChanged;
            _sizeToggles.Remove(t);
        }

        var existing = _sizeToggles.Select(t => t.Size).ToHashSet();
        foreach (var size in target)
        {
            if (existing.Contains(size))
                continue;
            var toggle = new SizeToggle(size, true, true);
            toggle.PropertyChanged += Toggle_PropertyChanged;
            InsertSorted(toggle);
        }

        UpdateAvailability();
        var topKeep = _sizeToggles
                      .Where(t => t is { Size: >= 256, IsChecked: true })
                      .MaxBy(t => t.Size);
        if (topKeep != null)
            EnforceTopFrameExclusivity(topKeep);
        RebuildThumbs();
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
            var col = CollectionFor(thumb.Size);
            var idx = col.IndexOf(thumb);
            thumb.Replace(resized);
            col.RemoveAt(idx);
            col.Insert(idx, thumb);
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