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
    public const string InstallerAssetName = "ArbuzTweaker-Setup.msi";
    public const string PortableAssetName = "ArbuzTweaker-Portable.zip";
    public const string ChecksumsAssetName = "SHA256SUMS.txt";
    private static readonly TimeSpan UpdateCheckTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(90);

    private readonly string _currentVersion;
    private readonly string _downloadPath;

    public string CurrentVersion => _currentVersion;
    public string DownloadPath => _downloadPath;

    public UpdateService(string currentVersion)
    {
        _currentVersion = currentVersion;
        _downloadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArbuzTweaker",
            "Updates");
    }

    public async Task<(bool hasUpdate, string? newVersion, string? downloadUrl, string? assetName)> CheckForUpdateAsync()
    {
        var result = await CheckForUpdateDetailsAsync();
        return (result.HasUpdate, result.NewVersion, result.DownloadUrl, result.AssetName);
    }

    public async Task<UpdateCheckResult> CheckForUpdateDetailsAsync()
    {
        try
        {
            var github = new GitHubClient(new ProductHeaderValue("ArbuzTweaker"));
            var releases = await github.Repository.Release.GetAll(Owner, Repo).WaitAsync(UpdateCheckTimeout);
            
            if (releases.Count == 0)
                return UpdateCheckResult.NoUpdate;

            var latest = releases[0];
            var latestVersion = latest.TagName.TrimStart('v');

            if (CompareVersions(latestVersion, _currentVersion) > 0)
            {
                var asset = latest.Assets.FirstOrDefault(a => string.Equals(a.Name, InstallerAssetName, StringComparison.OrdinalIgnoreCase))
                    ?? latest.Assets.FirstOrDefault(a => string.Equals(a.Name, PortableAssetName, StringComparison.OrdinalIgnoreCase))
                    ?? latest.Assets.FirstOrDefault(a => a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                var expectedSha256 = asset == null ? null : await GetExpectedSha256Async(latest, asset);
                return new UpdateCheckResult(true, latestVersion, asset?.BrowserDownloadUrl, asset?.Name, expectedSha256);
            }

            return UpdateCheckResult.NoUpdate;
        }
        catch
        {
            return UpdateCheckResult.NoUpdate;
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

    public UpdateLaunchResult LaunchDownloadedUpdate(
        string filePath,
        int currentProcessId,
        string restartExecutablePath,
        string? expectedVersion = null)
    {
        try
        {
            if (!File.Exists(filePath))
                return UpdateLaunchResult.Failure("Файл обновления не найден.");

            var extension = Path.GetExtension(filePath);
            if (extension.Equals(".msi", StringComparison.OrdinalIgnoreCase))
                return LaunchMsiSelfUpdate(filePath, currentProcessId, restartExecutablePath, expectedVersion);

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
            return UpdateLaunchResult.Success(false, "Файл обновления открыт.");
        }
        catch
        {
            return UpdateLaunchResult.Failure("Не удалось запустить обновление.");
        }
    }

    public bool OpenDownloadFolder()
    {
        try
        {
            Directory.CreateDirectory(_downloadPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = _downloadPath,
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

    public bool VerifyFileSha256(string filePath, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
            return true;

        var actualSha256 = GetFileSha256(filePath);
        return string.Equals(actualSha256, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
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

    private static async Task<string?> GetExpectedSha256Async(Release release, ReleaseAsset targetAsset)
    {
        var checksumAsset = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, ChecksumsAssetName, StringComparison.OrdinalIgnoreCase));
        if (checksumAsset == null || string.IsNullOrWhiteSpace(checksumAsset.BrowserDownloadUrl))
            return null;

        try
        {
            using var client = new HttpClient
            {
                Timeout = UpdateCheckTimeout
            };

            var content = await client.GetStringAsync(checksumAsset.BrowserDownloadUrl);
            return ParseSha256FromChecksums(content, targetAsset.Name);
        }
        catch
        {
            return null;
        }
    }

    private static string? ParseSha256FromChecksums(string content, string assetName)
    {
        foreach (var rawLine in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("#", StringComparison.Ordinal) ||
                !line.Contains(assetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                continue;

            var candidate = parts[0].TrimStart('*');
            if (candidate.Length == 64 && candidate.All(Uri.IsHexDigit))
                return candidate.ToUpperInvariant();
        }

        return null;
    }

    private UpdateLaunchResult LaunchMsiSelfUpdate(
        string msiPath,
        int currentProcessId,
        string restartExecutablePath,
        string? expectedVersion)
    {
        Directory.CreateDirectory(_downloadPath);

        var scriptPath = Path.Combine(_downloadPath, "Install-ArbuzTweaker-Update.ps1");
        var logPath = Path.Combine(_downloadPath, "install-update.log");
        var script = string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Continue'",
            "$msi = " + ToPowerShellSingleQuoted(msiPath),
            "$app = " + ToPowerShellSingleQuoted(restartExecutablePath),
            "$log = " + ToPowerShellSingleQuoted(logPath),
            "$expectedVersion = " + ToPowerShellSingleQuoted(expectedVersion ?? string.Empty),
            "$pidToWait = " + currentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "try { Wait-Process -Id $pidToWait -Timeout 90 -ErrorAction SilentlyContinue } catch { }",
            "$arguments = '/i \"' + $msi + '\" /passive /norestart /l*v \"' + $log + '\"'",
            "$process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $arguments -Wait -PassThru",
            "$exitCode = if ($null -ne $process) { $process.ExitCode } else { 1 }",
            "if ($exitCode -eq 0 -or $exitCode -eq 3010) {",
            "    $deadline = (Get-Date).AddSeconds(90)",
            "    while ((Get-Date) -lt $deadline) {",
            "        if (Test-Path -LiteralPath $app) {",
            "            if ([string]::IsNullOrWhiteSpace($expectedVersion)) { break }",
            "            try {",
            "                $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($app).FileVersion",
            "                if ($fileVersion -like ($expectedVersion + '*')) { break }",
            "            } catch { }",
            "        }",
            "        Start-Sleep -Seconds 1",
            "    }",
            "    if (Test-Path -LiteralPath $app) { Start-Process -FilePath $app }",
            "} else {",
            "    Start-Process -FilePath 'explorer.exe' -ArgumentList ('/select,\"' + $msi + '\"')",
            "}",
            "exit $exitCode",
            string.Empty
        });

        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        return UpdateLaunchResult.Success(
            true,
            "Установщик обновления запущен. ArbuzTweaker закроется, обновится и запустится снова.");
    }

    private static string ToPowerShellSingleQuoted(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }
}

public sealed record UpdateCheckResult(
    bool HasUpdate,
    string? NewVersion,
    string? DownloadUrl,
    string? AssetName,
    string? ExpectedSha256)
{
    public static UpdateCheckResult NoUpdate { get; } = new(false, null, null, null, null);
}

public sealed record UpdateLaunchResult(
    bool Started,
    bool ShouldCloseApplication,
    string Message)
{
    public static UpdateLaunchResult Success(bool shouldCloseApplication, string message)
    {
        return new UpdateLaunchResult(true, shouldCloseApplication, message);
    }

    public static UpdateLaunchResult Failure(string message)
    {
        return new UpdateLaunchResult(false, false, message);
    }
}
