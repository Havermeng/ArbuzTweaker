using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Octokit;

namespace ArbuzTweaker;

public class UpdateService
{
    public const string Owner = "Havermeng";
    public const string Repo = "ArbuzTweaker";
    public const string InstallerAssetName = "ArbuzTweaker-Setup.exe";
    public const string PortableAssetName = "ArbuzTweaker-Portable.zip";

    private readonly string _currentVersion;
    private readonly string _downloadPath;

    public UpdateService(string currentVersion)
    {
        _currentVersion = currentVersion;
        _downloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updates");
    }

    public async Task<(bool hasUpdate, string? newVersion, string? downloadUrl, string? assetName)> CheckForUpdateAsync()
    {
        try
        {
            var github = new GitHubClient(new ProductHeaderValue("ArbuzTweaker"));
            var releases = await github.Repository.Release.GetAll(Owner, Repo);
            
            if (releases.Count == 0)
                return (false, null, null, null);

            var latest = releases[0];
            var latestVersion = latest.TagName.TrimStart('v');

            if (CompareVersions(latestVersion, _currentVersion) > 0)
            {
                var asset = latest.Assets.FirstOrDefault(a => string.Equals(a.Name, InstallerAssetName, StringComparison.OrdinalIgnoreCase))
                    ?? latest.Assets.FirstOrDefault(a => string.Equals(a.Name, PortableAssetName, StringComparison.OrdinalIgnoreCase))
                    ?? latest.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                return (true, latestVersion, asset?.BrowserDownloadUrl, asset?.Name);
            }

            return (false, null, null, null);
        }
        catch
        {
            return (false, null, null, null);
        }
    }

    public async Task<string?> DownloadUpdateAsync(string url)
    {
        try
        {
            Directory.CreateDirectory(_downloadPath);
            var fileName = Path.GetFileName(url);
            var filePath = Path.Combine(_downloadPath, fileName);

            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(filePath, bytes);

            return filePath;
        }
        catch
        {
            return null;
        }
    }

    public bool LaunchDownloadedUpdate(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private int CompareVersions(string v1, string v2)
    {
        var parts1 = v1.Split('.').Select(int.Parse).ToArray();
        var parts2 = v2.Split('.').Select(int.Parse).ToArray();

        for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
        {
            int p1 = i < parts1.Length ? parts1[i] : 0;
            int p2 = i < parts2.Length ? parts2[i] : 0;
            if (p1 > p2) return 1;
            if (p1 < p2) return -1;
        }
        return 0;
    }
}
