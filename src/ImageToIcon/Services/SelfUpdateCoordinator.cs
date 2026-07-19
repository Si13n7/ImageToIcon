namespace ImageToIcon.Services;

public sealed class SelfUpdateCoordinator
{
    private readonly Settings _settings;
    private readonly CancellationToken _shutdownToken;

    public SelfUpdateCoordinator(Settings settings, CancellationToken shutdownToken)
    {
        _settings = settings;
        _shutdownToken = shutdownToken;

        if (Version.TryParse(settings.PendingUpdateVersion, out var cached))
            Pending = new SelfUpdateInfo(cached);
    }

    public SelfUpdateInfo? Pending { get; private set; }

    public async Task<SelfUpdateInfo?> CheckAsync()
    {
        Pending = await SelfUpdater.CheckAsync(_settings, _shutdownToken);
        return Pending;
    }

    public Task ApplyAsync(IProgress<int>? progress, Func<string, string, string, Task<bool>>? onHashMismatch = null)
    {
        return SelfUpdater.ApplyAsync(_settings, progress, onHashMismatch, _shutdownToken);
    }
}