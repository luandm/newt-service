using System.Net.Http.Json;
using System.Runtime.InteropServices;

namespace NewtService.Core;

public class NewtUpdater : IDisposable
{
    private readonly HttpClient _httpClient;
    
    public event Action<string>? OnLog;

    public NewtUpdater()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NewtService/1.0");
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync(bool includePrerelease = false)
    {
        try
        {
            var response = await _httpClient.GetAsync("https://api.github.com/repos/fosrl/newt/releases");
            if (!response.IsSuccessStatusCode)
            {
                Log($"GitHub API error: {response.StatusCode}");
                return null;
            }
            
            var releases = await response.Content.ReadFromJsonAsync<List<GitHubRelease>>();
            
            if (releases == null || releases.Count == 0)
            {
                Log("No releases found");
                return null;
            }

            var release = includePrerelease 
                ? releases.FirstOrDefault() 
                : releases.FirstOrDefault(r => !r.Prerelease);
                
            if (release != null)
                Log($"Found release: {release.TagName}");
                
            return release;
        }
        catch (Exception ex)
        {
            Log($"Failed to fetch releases: {ex.Message}");
            return null;
        }
    }

    public string? GetCurrentVersion()
    {
        if (!File.Exists(ServiceConstants.VersionFilePath))
            return null;
        
        return File.ReadAllText(ServiceConstants.VersionFilePath).Trim();
    }

    public async Task<bool> IsUpdateAvailableAsync(bool includePrerelease = false)
    {
        var latest = await GetLatestReleaseAsync(includePrerelease);
        if (latest == null) return false;
        
        var current = GetCurrentVersion();
        return current != latest.TagName;
    }

    public async Task<bool> DownloadAndInstallAsync(GitHubRelease release, IProgress<double>? progress = null)
    {
        var asset = GetWindowsAsset(release);
        if (asset == null)
        {
            Log("No compatible Windows asset found");
            return false;
        }

        try
        {
            Directory.CreateDirectory(ServiceConstants.AppDataPath);
            
            var tempPath = Path.Combine(Path.GetTempPath(), asset.Name);
            
            Log($"Downloading {asset.Name}...");
            await DownloadFileAsync(asset.DownloadUrl, tempPath, progress);
            
            Log("Installing...");
            File.Copy(tempPath, ServiceConstants.NewtExecutablePath, overwrite: true);
            
            File.WriteAllText(ServiceConstants.VersionFilePath, release.TagName);
            
            try { File.Delete(tempPath); } catch { }
            
            Log($"Updated to version {release.TagName}");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Update failed: {ex.Message}");
            return false;
        }
    }

    private GitHubAsset? GetWindowsAsset(GitHubRelease release)
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            _ => "amd64"
        };
        
        var expectedName = $"newt_windows_{arch}.exe";
        
        if (release.Assets?.Count > 0)
        {
            Log($"Available assets: {string.Join(", ", release.Assets.Select(a => a.Name))}");
        }
        else
        {
            Log("No assets in release");
            return null;
        }
        
        var asset = release.Assets.FirstOrDefault(a => 
            a.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase));
            
        if (asset == null)
            Log($"No asset matching '{expectedName}'");
        else
            Log($"Selected asset: {asset.Name}");
            
        return asset;
    }

    private async Task DownloadFileAsync(string url, string destination, IProgress<double>? progress)
    {
        Log($"Downloading from: {url}");
        
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Download failed: {response.StatusCode}");
        }
        
        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        Log($"Download size: {totalBytes / 1024 / 1024:F1} MB");
        
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
        
        Log("Download complete");
    }

    private void Log(string message)
    {
        AppLogger.Info($"[Updater] {message}");
        OnLog?.Invoke(message);
    }

    public void Dispose() => _httpClient.Dispose();
}

