using System;
using System.IO;
using System.IO.Compression;

namespace SkyrimAEBackup;

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

    public static int RestoreAEContent(string zipPath, string skyrimPath, IProgress<string>? progress = null)
    {
        var dataFolder = Path.Combine(skyrimPath, "Data");
        if (!Directory.Exists(dataFolder))
            throw new DirectoryNotFoundException($"Data folder not found: {dataFolder}");

        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Backup zip not found", zipPath);

        using var zip = ZipFile.OpenRead(zipPath);
        int count = 0;
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // skip directories
            // Safety: only extract files we recognize as AE content
            if (!AEContentDetector.IsAEFile(entry.Name))
            {
                progress?.Report($"Skipping unrecognized: {entry.Name}");
                continue;
            }
            var dest = Path.Combine(dataFolder, entry.Name);
            progress?.Report($"Restoring: {entry.Name}");
            entry.ExtractToFile(dest, overwrite: true);
            count++;
        }
        return count;
    }
}
