using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SkyrimAEBackup.Core;

public record RestoreProgress(int Current, int Total, string Message);
public record BackupProgress(int Current, int Total, string Message);

public static class BackupManager
{
    public static int BackupAEContent(string skyrimPath, string outputZipPath, IProgress<string>? progress = null)
    {
        var dataFolder = Path.Combine(skyrimPath, "Data");
        if (!Directory.Exists(dataFolder))
            throw new DirectoryNotFoundException($"Data folder not found: {dataFolder}");

        var files = AEContentDetector.FindAEFiles(dataFolder);
        if (files.Count == 0)
            throw new InvalidOperationException("No AE / Creation Club content found in Data folder.");

        if (File.Exists(outputZipPath)) File.Delete(outputZipPath);

        using var zip = ZipFile.Open(outputZipPath, ZipArchiveMode.Create);
        int count = 0;
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            progress?.Report($"Adding: {name}");
            zip.CreateEntryFromFile(file, name, CompressionLevel.Optimal);
            count++;
        }
        return count;
    }

    /// <summary>Backup a specific list of files (by full path) into a zip with structured progress.</summary>
    public static int BackupSelectedFiles(
        IList<string> filePaths,
        string outputZipPath,
        IProgress<BackupProgress>? progress = null)
    {
        if (filePaths.Count == 0)
            throw new InvalidOperationException("No files selected for backup.");

        if (File.Exists(outputZipPath)) File.Delete(outputZipPath);

        using var zip = ZipFile.Open(outputZipPath, ZipArchiveMode.Create);
        int current = 0;
        int total = filePaths.Count;
        foreach (var file in filePaths)
        {
            current++;
            var name = Path.GetFileName(file);
            progress?.Report(new BackupProgress(current, total, $"Adding: {name}"));
            zip.CreateEntryFromFile(file, name, CompressionLevel.Optimal);
        }
        progress?.Report(new BackupProgress(current, total, $"Done. {current} file(s) added."));
        return current;
    }

    /// <summary>List AE-matching filenames inside a backup zip (for selection UI).</summary>
    public static List<string> ListAEEntries(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Backup zip not found", zipPath);

        using var zip = ZipFile.OpenRead(zipPath);
        return zip.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name) && AEContentDetector.IsAEFile(e.Name))
            .Select(e => e.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Legacy: restore all AE entries from zip.</summary>
    public static int RestoreAEContent(string zipPath, string skyrimPath, IProgress<string>? progress = null)
    {
        var all = ListAEEntries(zipPath);
        return RestoreSelectedEntries(zipPath, skyrimPath, all,
            progress == null ? null : new Progress<RestoreProgress>(p => progress.Report(p.Message)));
    }

    /// <summary>Restore a specific subset of AE entries with detailed progress reporting.</summary>
    public static int RestoreSelectedEntries(
        string zipPath,
        string skyrimPath,
        IList<string> selectedNames,
        IProgress<RestoreProgress>? progress = null)
    {
        var dataFolder = Path.Combine(skyrimPath, "Data");
        if (!Directory.Exists(dataFolder))
            throw new DirectoryNotFoundException($"Data folder not found: {dataFolder}");
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Backup zip not found", zipPath);

        var selected = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);
        using var zip = ZipFile.OpenRead(zipPath);

        // Build the actual list of entries to extract (filtered + AE-safe)
        var toExtract = zip.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name)
                        && selected.Contains(e.Name)
                        && AEContentDetector.IsAEFile(e.Name))
            .ToList();

        int total = toExtract.Count;
        int current = 0;
        foreach (var entry in toExtract)
        {
            current++;
            progress?.Report(new RestoreProgress(current, total, $"Restoring: {entry.Name}"));
            var dest = Path.Combine(dataFolder, entry.Name);
            entry.ExtractToFile(dest, overwrite: true);
        }
        progress?.Report(new RestoreProgress(current, total, $"Done. {current} file(s) restored."));
        return current;
    }
}
