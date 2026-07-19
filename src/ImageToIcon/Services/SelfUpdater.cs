using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace ImageToIcon.Services;

public sealed class SelfUpdateInfo(Version version)
{
    public Version Version { get; } = version;
}

public static class SelfUpdater
{
    private const string Repo = "koryboc/ImageToIcon";
    private const string LinuxBinary = "ImageToIcon";
    private const string WindowsBinary = "ImageToIcon.exe";
    private const string LinuxAssetKey = "linux-x64";
    private const string WindowsAssetKey = "win-x64";
    private const string BothAssetKey = "both-platforms-x64";

    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromDays(7);

    public static async Task<SelfUpdateInfo?> CheckAsync(Settings settings, CancellationToken ct)
    {
        var current = Assembly.GetEntryAssembly()?.GetName().Version;
        if (current == null)
            return null;

        var cachedVersion = TryParseVersion(settings.PendingUpdateVersion);
        var withinThrottle = settings.LastUpdateCheckUtc > DateTime.MinValue
                             && DateTime.UtcNow - settings.LastUpdateCheckUtc < MinRefreshInterval;

        if (withinThrottle)
        {
            if (cachedVersion != null && cachedVersion > current)
                return new SelfUpdateInfo(cachedVersion);

            if (!string.IsNullOrEmpty(settings.PendingUpdateVersion))
            {
                settings.PendingUpdateVersion = "";
                settings.Save();
            }

            return null;
        }

        var release = await GitHubClient.FetchLatestReleaseAsync(Repo, ct);
        settings.LastUpdateCheckUtc = DateTime.UtcNow;

        if (release == null || !Version.TryParse(release.TagName, out var latest) || latest <= current)
        {
            settings.PendingUpdateVersion = "";
            settings.Save();
            return null;
        }

        settings.PendingUpdateVersion = latest.ToString();
        settings.Save();
        return new SelfUpdateInfo(latest);
    }

    public static async Task<string?> FetchLatestBodyAsync(CancellationToken ct)
    {
        var release = await GitHubClient.FetchLatestReleaseAsync(Repo, ct, false);
        return release?.Body;
    }

    public static async Task ApplyAsync(
        Settings settings,
        IProgress<int>? progress,
        Func<string, string, string, Task<bool>>? onHashMismatch,
        CancellationToken ct)
    {
        // Always fetch fresh — never trust cached pending metadata for the actual download.
        var release = await GitHubClient.FetchLatestReleaseAsync(Repo, ct, false);
        if (release == null || !Version.TryParse(release.TagName, out var latest))
            throw new IOException("Failed to fetch latest release from GitHub.");

        var current = Assembly.GetEntryAssembly()?.GetName().Version;
        if (current != null && latest <= current)
            throw new IOException($"No newer release available (installed {current}, latest {latest}).");

        var installDir = ResolveInstallDir();
        var asset = PickAssetForLocalBinaries(release, installDir);
        if (asset == null)
            throw new IOException("No matching release asset for the installed binaries.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"ImageToIcon-{latest}");
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
        Directory.CreateDirectory(tempDir);

        var zipPath = Path.Combine(tempDir, "update.zip");
        var pct = progress == null ? null : new Progress<double>(p => progress.Report((int)(p * 100)));
        if (!await GitHubClient.DownloadAssetAsync(asset.DownloadUrl, zipPath, pct, ct))
            throw new IOException($"Failed to download {asset.Name}.");

        if (!string.IsNullOrEmpty(asset.Sha256Hex) && !VerifySha256(zipPath, asset.Sha256Hex))
        {
            var proceed = onHashMismatch != null && await onHashMismatch(asset.Name, asset.Sha256Hex, zipPath);
            if (!proceed)
            {
                TryDelete(zipPath);
                throw new IOException($"Hash mismatch for {asset.Name}.");
            }
        }

        var extractRoot = Path.GetFullPath(tempDir + Path.DirectorySeparatorChar);
        await using (var archive = await ZipFile.OpenReadAsync(zipPath, ct))
        {
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;
                var dest = Path.GetFullPath(Path.Combine(tempDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!dest.StartsWith(extractRoot, StringComparison.Ordinal))
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await using var input = await entry.OpenAsync(ct);
                await using var output = File.Create(dest);
                await input.CopyToAsync(output, ct);
            }
        }

        TryDelete(zipPath);

        var replacements = new List<(string source, string destName)>();
        foreach (var name in new[] { LinuxBinary, WindowsBinary })
        {
            var match = Directory.GetFiles(tempDir, name, SearchOption.AllDirectories).FirstOrDefault();
            if (match != null)
                replacements.Add((match, name));
        }

        if (replacements.Count == 0)
            throw new IOException("Update archive did not contain any known binary.");

        settings.PendingUpdateVersion = "";
        settings.LastUpdateCheckUtc = DateTime.UtcNow;
        settings.Save();

        if (OperatingSystem.IsWindows())
            ApplyWindows(installDir, replacements, latest);
        else if (OperatingSystem.IsLinux())
            ApplyLinux(installDir, replacements);
    }

    private static string ResolveInstallDir()
    {
        var exe = Environment.ProcessPath;
        return !string.IsNullOrEmpty(exe)
            ? Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory
            : AppContext.BaseDirectory;
    }

    private static GhAsset? PickAssetForLocalBinaries(GhRelease release, string installDir)
    {
        var hasLinux = File.Exists(Path.Combine(installDir, LinuxBinary));
        var hasWindows = File.Exists(Path.Combine(installDir, WindowsBinary));

        string key;
        if (hasLinux && hasWindows)
            key = BothAssetKey;
        else if (hasLinux)
            key = LinuxAssetKey;
        else if (hasWindows)
            key = WindowsAssetKey;
        else
            return null;

        return release.Assets.FirstOrDefault(a =>
                                                 a.Name.Contains(key, StringComparison.OrdinalIgnoreCase)
                                                 && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    private static Version? TryParseVersion(string s)
    {
        return Version.TryParse(s, out var v) ? v : null;
    }

    private static bool VerifySha256(string filePath, string expectedHex)
    {
        using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return actual.Equals(expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // best-effort
        }
    }

    [SupportedOSPlatform("linux")]
    private static void ApplyLinux(string installDir, List<(string source, string destName)> replacements)
    {
        var exe = Environment.ProcessPath ?? Path.Combine(installDir, LinuxBinary);
        var script = new StringBuilder();
        script.AppendLine("#!/bin/sh");
        script.AppendLine("sleep 2");
        foreach (var (source, destName) in replacements)
        {
            var dest = Path.Combine(installDir, destName);
            script.AppendLine($"cp -f \"{source}\" \"{dest}\"");
            if (destName == LinuxBinary)
                script.AppendLine($"chmod +x \"{dest}\"");
        }

        script.AppendLine($"cd \"{installDir}\"");
        script.AppendLine($"exec \"{exe}\"");

        var scriptPath = Path.Combine(Path.GetTempPath(), "imagetoicon-restart.sh");
        File.WriteAllText(scriptPath, script.ToString());
        File.SetUnixFileMode(
            scriptPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        Environment.Exit(0);
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindows(string installDir, List<(string source, string destName)> replacements, Version version)
    {
        var batPath = Path.Combine(Path.GetTempPath(), $"ImageToIcon-update-{version}.bat");
        var exePath = Path.Combine(installDir, WindowsBinary);
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("cd /D \"%~dp0\"");
        sb.AppendLine("timeout /t 3 /nobreak >nul");
        sb.AppendLine($"taskkill /f /im {WindowsBinary} 2>nul");
        sb.AppendLine("timeout /t 2 /nobreak >nul");
        foreach (var (source, destName) in replacements)
        {
            var dest = Path.Combine(installDir, destName);
            sb.AppendLine($"copy /y \"{source}\" \"{dest}\"");
        }

        sb.AppendLine($"start \"\" \"{exePath}\"");
        sb.AppendLine("del \"%~f0\"");
        File.WriteAllText(batPath, sb.ToString());
        Process.Start(new ProcessStartInfo(batPath)
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        Environment.Exit(0);
    }
}