using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SkyrimAEBackup;

/// <summary>
/// Identifies Anniversary Edition / Creation Club content.
/// CC plugins follow the pattern: cc[DEV]SSE[NUM].esl/esm/esp (e.g., ccBGSSSE001-Fish.esl)
/// Plus _ResourcePack.esl/bsa which is the AE upgrade pack.
/// </summary>
public static class AEContentDetector
{
    // CC files: cc + 3-letter dev code + "SSE" + 3 digits + optional "-name" + .ext
    private static readonly Regex CcFilePattern = new(
        @"^cc[A-Z0-9]{3}SSE\d{3}.*\.(esl|esm|esp|bsa)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ResourcePackPattern = new(
        @"^_ResourcePack.*\.(esl|esm|esp|bsa)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsAEFile(string filename)
    {
        var name = Path.GetFileName(filename);
        return CcFilePattern.IsMatch(name) || ResourcePackPattern.IsMatch(name);
    }

    public static bool IsAEPlugin(string filename)
    {
        var name = Path.GetFileName(filename);
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (ext != ".esl" && ext != ".esm" && ext != ".esp") return false;
        return CcFilePattern.IsMatch(name) || ResourcePackPattern.IsMatch(name);
    }

    /// <summary>
    /// Find all AE/CC files in the Data folder (plugins + matching BSAs).
    /// </summary>
    public static List<string> FindAEFiles(string dataFolder)
    {
        if (!Directory.Exists(dataFolder)) return new List<string>();
        return Directory.EnumerateFiles(dataFolder)
            .Where(f => IsAEFile(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Get just the plugin filenames (esl/esm/esp), no BSAs.
    /// Used for plugins.txt manipulation.
    /// </summary>
    public static List<string> FindAEPluginNames(string dataFolder)
    {
        if (!Directory.Exists(dataFolder)) return new List<string>();
        return Directory.EnumerateFiles(dataFolder)
            .Select(Path.GetFileName)
            .Where(n => n != null && IsAEPlugin(n))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
