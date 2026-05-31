using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SkyrimAEBackup.Core;

public enum ContentCategory { AE, OtherCC, Unknown }

/// <summary>
/// Identifies Anniversary Edition / Creation Club content.
/// Categorization uses Skyrim.ccc (the official AE plugin manifest in game root)
/// when available, falling back to regex matching for cc*SSE* / _ResourcePack.*
/// </summary>
public static class AEContentDetector
{
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
    /// Read Skyrim.ccc — text file listing the canonical AE plugin filenames, one per line.
    /// Returns empty set if file missing (SE-only install).
    /// </summary>
    public static HashSet<string> ReadCccList(string skyrimRoot)
    {
        var cccPath = Path.Combine(skyrimRoot, "Skyrim.ccc");
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(cccPath)) return set;

        try
        {
            foreach (var line in File.ReadAllLines(cccPath))
            {
                var l = line.Trim();
                if (string.IsNullOrWhiteSpace(l) || l.StartsWith("#")) continue;
                set.Add(l);
            }
        }
        catch { /* unreadable .ccc treated as missing */ }
        return set;
    }

    /// <summary>
    /// Classify a file based on the AE .ccc list.
    /// AE: file's base name matches a plugin in Skyrim.ccc (covers both plugin + companion BSA).
    /// OtherCC: matches cc* pattern but not in .ccc list (e.g., separately purchased Creations).
    /// </summary>
    public static ContentCategory Classify(string filename, HashSet<string> aePlugins)
    {
        var name = Path.GetFileName(filename);
        if (ResourcePackPattern.IsMatch(name)) return ContentCategory.AE;

        if (aePlugins.Count > 0)
        {
            var baseName = Path.GetFileNameWithoutExtension(name);
            // Match by base name so the BSA companion of an AE plugin counts as AE
            bool isAE = aePlugins.Any(ae =>
                Path.GetFileNameWithoutExtension(ae)
                    .Equals(baseName, StringComparison.OrdinalIgnoreCase));
            if (isAE) return ContentCategory.AE;
        }

        if (CcFilePattern.IsMatch(name)) return ContentCategory.OtherCC;
        return ContentCategory.Unknown;
    }

    /// <summary>Find all AE/CC files in Data, returned with their category.</summary>
    public static List<(string FullPath, string Name, ContentCategory Category)>
        FindCategorizedFiles(string skyrimRoot)
    {
        var dataFolder = Path.Combine(skyrimRoot, "Data");
        if (!Directory.Exists(dataFolder)) return new();

        var aePlugins = ReadCccList(skyrimRoot);

        return Directory.EnumerateFiles(dataFolder)
            .Where(IsAEFile)
            .Select(f => (FullPath: f, Name: Path.GetFileName(f), Category: Classify(f, aePlugins)))
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Legacy method — kept for restore-side compatibility.</summary>
    public static List<string> FindAEFiles(string dataFolder)
    {
        if (!Directory.Exists(dataFolder)) return new();
        return Directory.EnumerateFiles(dataFolder)
            .Where(IsAEFile)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// AE plugins listed in .ccc but missing from the Data folder.
    /// (BSA companions are not tracked separately — only plugin-level missing detection.)
    /// </summary>
    public static List<string> FindMissingAEPlugins(string skyrimRoot)
    {
        var aePlugins = ReadCccList(skyrimRoot);
        if (aePlugins.Count == 0) return new();

        var dataFolder = Path.Combine(skyrimRoot, "Data");
        if (!Directory.Exists(dataFolder)) return aePlugins.OrderBy(s => s).ToList();

        var existing = Directory.EnumerateFiles(dataFolder)
            .Select(f => Path.GetFileName(f))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return aePlugins
            .Where(p => !existing.Contains(p))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
