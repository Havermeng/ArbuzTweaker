using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ArbuzTweaker;

internal static class SteamUserResolver
{
    private const long SteamId64Base = 76561197960265728;

    public static List<string> GetTargetLocalConfigPaths(string steamPath, string? preferredAccountId32 = null)
    {
        var result = new List<string>();
        var userPath = GetPrimarySteamUserPath(steamPath, preferredAccountId32);
        if (string.IsNullOrWhiteSpace(userPath))
            return result;

        var configPath = Path.Combine(userPath, "config", "localconfig.vdf");
        if (File.Exists(configPath))
            result.Add(configPath);

        return result;
    }

    public static string? GetPrimarySteamUserPath(string steamPath, string? preferredAccountId32 = null)
    {
        var userDataPath = Path.Combine(steamPath, "userdata");
        if (!Directory.Exists(userDataPath))
            return null;

        if (!string.IsNullOrWhiteSpace(preferredAccountId32))
        {
            var preferredPath = Path.Combine(userDataPath, preferredAccountId32);
            if (Directory.Exists(preferredPath))
                return preferredPath;
        }

        var activeUserId = GetActiveUserAccountId32();
        if (!string.IsNullOrWhiteSpace(activeUserId))
        {
            var activeUserPath = Path.Combine(userDataPath, activeUserId);
            if (Directory.Exists(activeUserPath))
                return activeUserPath;
        }

        var recentUserId = GetMostRecentAccountId32(steamPath);
        if (!string.IsNullOrWhiteSpace(recentUserId))
        {
            var recentUserPath = Path.Combine(userDataPath, recentUserId);
            if (Directory.Exists(recentUserPath))
                return recentUserPath;
        }

        var userDirectories = Directory.GetDirectories(userDataPath);
        return userDirectories.Length == 1 ? userDirectories[0] : null;
    }

    public static List<SteamUserInfo> GetSteamUsers(string steamPath)
    {
        var userDataPath = Path.Combine(steamPath, "userdata");
        if (!Directory.Exists(userDataPath))
            return new List<SteamUserInfo>();

        var activeUserId = GetActiveUserAccountId32();
        var metadata = GetLoginUserMetadata(steamPath);
        var users = new List<SteamUserInfo>();

        foreach (var directory in Directory.GetDirectories(userDataPath))
        {
            var accountId32 = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(accountId32) || !accountId32.All(char.IsDigit))
                continue;

            var hasMetadata = metadata.TryGetValue(accountId32, out var userMetadata);
            var localConfigPath = Path.Combine(directory, "config", "localconfig.vdf");
            var videoConfigPath = Path.Combine(directory, "570", "local", "cfg", "video.txt");
            var isActive = string.Equals(accountId32, activeUserId, StringComparison.OrdinalIgnoreCase);
            var displayName = !hasMetadata || string.IsNullOrWhiteSpace(userMetadata?.PersonaName)
                ? $"Steam account {accountId32}"
                : userMetadata.PersonaName;

            users.Add(new SteamUserInfo(
                accountId32,
                directory,
                displayName,
                isActive,
                userMetadata?.MostRecent ?? false,
                File.Exists(localConfigPath) ? localConfigPath : null,
                File.Exists(videoConfigPath) ? videoConfigPath : null));
        }

        return users
            .OrderByDescending(user => user.IsActive)
            .ThenByDescending(user => user.IsMostRecent)
            .ThenBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? GetActiveUserAccountId32()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
            var activeUser = key?.GetValue("ActiveUser");
            return activeUser switch
            {
                int value when value > 0 => value.ToString(),
                uint value when value > 0 => value.ToString(),
                long value when value > 0 => value.ToString(),
                string value when uint.TryParse(value, out var parsed) && parsed > 0 => parsed.ToString(),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetMostRecentAccountId32(string steamPath)
    {
        try
        {
            var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(loginUsersPath))
                return null;

            var content = File.ReadAllText(loginUsersPath);
            var matches = Regex.Matches(
                content,
                "\\\"(?<steamId64>\\d{17})\\\"\\s*\\{(?<body>.*?)\\n\\s*\\}",
                RegexOptions.Singleline);

            var mostRecentUsers = matches
                .Select(match => new
                {
                    SteamId64 = match.Groups["steamId64"].Value,
                    Body = match.Groups["body"].Value,
                    Timestamp = GetTimestamp(match.Groups["body"].Value)
                })
                .Where(user => Regex.IsMatch(user.Body, "\\\"MostRecent\\\"\\s*\\\"1\\\""))
                .OrderByDescending(user => user.Timestamp)
                .ToList();

            foreach (var user in mostRecentUsers)
            {
                var accountId32 = ConvertSteamId64ToAccountId32(user.SteamId64);
                if (!string.IsNullOrWhiteSpace(accountId32))
                    return accountId32;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, SteamLoginUserMetadata> GetLoginUserMetadata(string steamPath)
    {
        var result = new Dictionary<string, SteamLoginUserMetadata>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(loginUsersPath))
                return result;

            var content = File.ReadAllText(loginUsersPath);
            var matches = Regex.Matches(
                content,
                "\\\"(?<steamId64>\\d{17})\\\"\\s*\\{(?<body>.*?)\\n\\s*\\}",
                RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var accountId32 = ConvertSteamId64ToAccountId32(match.Groups["steamId64"].Value);
                if (string.IsNullOrWhiteSpace(accountId32))
                    continue;

                var body = match.Groups["body"].Value;
                result[accountId32] = new SteamLoginUserMetadata(
                    GetQuotedVdfValue(body, "PersonaName"),
                    Regex.IsMatch(body, "\\\"MostRecent\\\"\\s*\\\"1\\\""),
                    GetTimestamp(body));
            }
        }
        catch
        {
        }

        return result;
    }

    private static long GetTimestamp(string body)
    {
        var match = Regex.Match(body, "\\\"Timestamp\\\"\\s*\\\"(?<timestamp>\\d+)\\\"");
        return match.Success && long.TryParse(match.Groups["timestamp"].Value, out var timestamp)
            ? timestamp
            : 0;
    }

    private static string? GetQuotedVdfValue(string body, string key)
    {
        var match = Regex.Match(body, $"\\\"{Regex.Escape(key)}\\\"\\s*\\\"(?<value>[^\\\"]*)\\\"");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? ConvertSteamId64ToAccountId32(string steamId64Text)
    {
        if (!long.TryParse(steamId64Text, out var steamId64))
            return null;

        var accountId32 = steamId64 - SteamId64Base;
        return accountId32 > 0 && accountId32 <= uint.MaxValue ? accountId32.ToString() : null;
    }
}

public sealed record SteamUserInfo(
    string AccountId32,
    string UserPath,
    string DisplayName,
    bool IsActive,
    bool IsMostRecent,
    string? LocalConfigPath,
    string? VideoConfigPath)
{
    public override string ToString()
    {
        var suffix = IsActive ? " (активный)" : IsMostRecent ? " (последний)" : string.Empty;
        return $"{DisplayName} [{AccountId32}]{suffix}";
    }
}

internal sealed record SteamLoginUserMetadata(string? PersonaName, bool MostRecent, long Timestamp);
