using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;

namespace NewtService.Core;

public class AppUpdater : IDisposable
{
    private readonly HttpClient _httpClient;
    private const string ReleasesApi = "https://api.github.com/repos/memesalot/newt-service/releases/latest";
    private const string ReleasesUrl = "https://github.com/memesalot/newt-service/releases";
    
    public event Action<string>? OnLog;

    public AppUpdater()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NewtService/1.0");
    }

    public static string? GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        if (version == null) return null;
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public async Task<AppRelease?> GetLatestReleaseAsync()
    {
        try
        {
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(ReleasesApi);
            if (release == null) return null;

            var msiAsset = release.Assets.FirstOrDefault(a => 
                a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));

            return new AppRelease
            {
                TagName = release.TagName,
                Version = release.TagName.TrimStart('v'),
                MsiDownloadUrl = msiAsset?.DownloadUrl,
                ReleaseUrl = ReleasesUrl
            };
        }
        catch (Exception ex)
        {
            Log($"Failed to check for app updates: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> IsUpdateAvailableAsync()
    {
        var current = GetCurrentVersion();
        if (current == null) return false;

        var latest = await GetLatestReleaseAsync();
        if (latest == null) return false;

        return CompareVersions(latest.Version, current) > 0;
    }

    public async Task<bool> DownloadAndInstallAsync(AppRelease release, IProgress<double>? progress = null)
    {
        if (string.IsNullOrEmpty(release.MsiDownloadUrl))
        {
            Log("No MSI download available");
            return false;
        }

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "NewtServiceSetup.msi");
            
            Log("Downloading update...");
            await DownloadFileAsync(release.MsiDownloadUrl, tempPath, progress);
            
            Log("Starting installer...");
            Process.Start(new ProcessStartInfo
            {
                FileName = "msiexec",
                Arguments = $"/i \"{tempPath}\" /passive",
                UseShellExecute = true
            });

            return true;
        }
        catch (Exception ex)
        {
            Log($"Update failed: {ex.Message}");
            return false;
        }
    }

    public void OpenReleasesPage()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = ReleasesUrl,
            UseShellExecute = true
        });
    }

    private async Task DownloadFileAsync(string url, string destination, IProgress<double>? progress)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        
        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        
        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        
        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;
        
        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalRead += bytesRead;
            
            if (totalBytes > 0)
                progress?.Report((double)totalRead / totalBytes * 100);
        }
    }

    private static int CompareVersions(string v1, string v2)
    {
        var parts1 = v1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var parts2 = v2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        
        for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
        {
            var p1 = i < parts1.Length ? parts1[i] : 0;
            var p2 = i < parts2.Length ? parts2[i] : 0;
            if (p1 != p2) return p1.CompareTo(p2);
        }
        return 0;
    }

    private void Log(string message) => OnLog?.Invoke(message);

    public void Dispose() => _httpClient.Dispose();
}

public class AppRelease
{
    public string TagName { get; set; } = "";
    public string Version { get; set; } = "";
    public string? MsiDownloadUrl { get; set; }
    public string ReleaseUrl { get; set; } = "";
}

