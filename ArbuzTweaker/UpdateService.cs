using System;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Octokit;

namespace ArbuzTweaker;

public class UpdateService
{
    public const string Owner = "Havermeng";
    public const string Repo = "ArbuzTweaker";
    public const string InstallerAssetName = "ArbuzTweaker-Setup.exe";
    public const string PortableAssetName = "ArbuzTweaker-Portable.zip";
    private static readonly TimeSpan UpdateCheckTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(90);

    private readonly string _currentVersion;
    private readonly string _downloadPath;

    public string CurrentVersion => _currentVersion;

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
            var releases = await github.Repository.Release.GetAll(Owner, Repo).WaitAsync(UpdateCheckTimeout);
            
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
            using var client = new HttpClient
            {
                Timeout = DownloadTimeout
            };

            var fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "ArbuzTweaker-update";

            var bytes = await client.GetByteArrayAsync(url);
            var filePath = Path.Combine(_downloadPath, fileName);
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

    public string GetFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    public bool HasAuthenticodeSignature(string filePath)
    {
        try
        {
            using var certificate = X509CertificateLoader.LoadCertificateFromFile(filePath);
            return certificate != null;
        }
        catch
        {
            return false;
        }
    }

    private static int CompareVersions(string v1, string v2)
    {
        var parts1 = ParseVersionParts(v1);
        var parts2 = ParseVersionParts(v2);

        for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
        {
            int p1 = i < parts1.Length ? parts1[i] : 0;
            int p2 = i < parts2.Length ? parts2[i] : 0;
            if (p1 > p2) return 1;
            if (p1 < p2) return -1;
        }
        return 0;
    }

    private static int[] ParseVersionParts(string version)
    {
        return version
            .Trim()
            .TrimStart('v', 'V')
            .Split(new[] { '.', '-', '+' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var numericPrefix = new string(part.TakeWhile(char.IsDigit).ToArray());
                return int.TryParse(numericPrefix, out var value) ? value : 0;
            })
            .ToArray();
    }
}
