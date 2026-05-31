using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SkyrimAEBackup.Core;

public static class SkyrimDetector
{
    private static readonly string[] BethesdaRegPaths =
    {
        @"SOFTWARE\WOW6432Node\Bethesda Softworks\Skyrim Special Edition",
        @"SOFTWARE\Bethesda Softworks\Skyrim Special Edition"
    };

    public static string? DetectInstallPath()
    {
        // Try Bethesda registry key
        foreach (var regPath in BethesdaRegPaths)
        {
            using var key = Registry.LocalMachine.OpenSubKey(regPath);
            if (key == null) continue;

            var path = key.GetValue("Installed Path") as string
                       ?? key.GetValue("installed path") as string;
            if (!string.IsNullOrWhiteSpace(path))
            {
                path = path.TrimEnd('\\', '/');
                if (Directory.Exists(path) && LooksLikeSkyrim(path))
                    return path;
            }
        }

        // Try Steam (AppID 489830)
        return DetectViaSteam();
    }

    private static string? DetectViaSteam()
    {
        string? steamPath = null;
        var steamKeys = new[]
        {
            @"SOFTWARE\WOW6432Node\Valve\Steam",
            @"SOFTWARE\Valve\Steam"
        };
        foreach (var sk in steamKeys)
        {
            using var key = Registry.LocalMachine.OpenSubKey(sk);
            steamPath = key?.GetValue("InstallPath") as string;
            if (!string.IsNullOrWhiteSpace(steamPath)) break;
        }
        if (string.IsNullOrWhiteSpace(steamPath)) return null;

        // Default library
        var defaultLib = Path.Combine(steamPath, "steamapps", "common", "Skyrim Special Edition");
        if (Directory.Exists(defaultLib) && LooksLikeSkyrim(defaultLib))
            return defaultLib;

        // Parse libraryfolders.vdf for other libraries
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) return null;

        try
        {
            var content = File.ReadAllText(vdf);
            var matches = Regex.Matches(content, "\"path\"\\s+\"([^\"]+)\"");
            foreach (Match m in matches)
            {
                var lib = m.Groups[1].Value.Replace(@"\\", @"\");
                var candidate = Path.Combine(lib, "steamapps", "common", "Skyrim Special Edition");
                if (Directory.Exists(candidate) && LooksLikeSkyrim(candidate))
                    return candidate;
            }
        }
        catch { /* ignore */ }

        return null;
    }

    public static bool LooksLikeSkyrim(string path)
    {
        if (!Directory.Exists(path)) return false;
        // Check for SkyrimSE.exe and Data folder
        var hasExe = File.Exists(Path.Combine(path, "SkyrimSE.exe"));
        var hasData = Directory.Exists(Path.Combine(path, "Data"));
        return hasExe && hasData;
    }
}
