using SkyrimAEBackup.Core;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace SkyrimAEBackup;

public partial class MainWindow : Window
{
    private AppSettings _settings = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Initialize();
    }

    private void Initialize()
    {
        _settings = AppSettings.Load();

        // Skyrim path: use saved value if valid, else auto-detect
        if (!string.IsNullOrWhiteSpace(_settings.SkyrimPath) && Directory.Exists(_settings.SkyrimPath))
        {
            SkyrimPathBox.Text = _settings.SkyrimPath;
            Log($"Skyrim folder: {_settings.SkyrimPath}");
        }
        else
        {
            var path = SkyrimDetector.DetectInstallPath();
            if (path != null)
            {
                SkyrimPathBox.Text = path;
                _settings.SkyrimPath = path;
                _settings.Save();
                Log($"Auto-detected Skyrim at: {path}");
            }
            else
            {
                Log("Could not auto-detect Skyrim installation. Click Browse to select manually.");
            }
        }

        // Backup folder: use saved value or default to Documents\SkyrimAEBackups
        if (!string.IsNullOrWhiteSpace(_settings.BackupFolder) && Directory.Exists(_settings.BackupFolder))
        {
            BackupFolderBox.Text = _settings.BackupFolder;
        }
        else
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var defaultBackup = Path.Combine(docs, "SkyrimAEBackups");
            BackupFolderBox.Text = defaultBackup;
            _settings.BackupFolder = defaultBackup;
            _settings.Save();
        }
        Log($"Backup folder: {BackupFolderBox.Text}");
    }

    private void BrowseSkyrim_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Skyrim Special Edition folder (contains SkyrimSE.exe)"
        };
        if (dlg.ShowDialog() == true)
        {
            if (!SkyrimDetector.LooksLikeSkyrim(dlg.FolderName))
            {
                var proceed = MessageBox.Show(
                    "This folder does not appear to contain SkyrimSE.exe and a Data folder. Use it anyway?",
                    "Folder Check", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (proceed != MessageBoxResult.Yes) return;
            }
            SkyrimPathBox.Text = dlg.FolderName;
            _settings.SkyrimPath = dlg.FolderName;
            _settings.Save();
            Log($"Skyrim folder set to: {dlg.FolderName}");
        }
    }

    private void BrowseBackup_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select folder to store backups",
            InitialDirectory = Directory.Exists(BackupFolderBox.Text) ? BackupFolderBox.Text : ""
        };
        if (dlg.ShowDialog() == true)
        {
            BackupFolderBox.Text = dlg.FolderName;
            _settings.BackupFolder = dlg.FolderName;
            _settings.Save();
            Log($"Backup folder set to: {dlg.FolderName}");
        }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSkyrim()) return;
        if (!ValidateBackupFolder()) return;

        var dlg = new BackupDialog(SkyrimPathBox.Text, BackupFolderBox.Text)
        {
            Owner = this
        };
        dlg.ShowDialog();
        Log("Backup dialog closed.");
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSkyrim()) return;

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Zip files (*.zip)|*.zip",
            Title = "Select backup zip",
            InitialDirectory = Directory.Exists(BackupFolderBox.Text) ? BackupFolderBox.Text : ""
        };
        if (dlg.ShowDialog() != true) return;

        var restoreWindow = new RestoreDialog(dlg.FileName, SkyrimPathBox.Text)
        {
            Owner = this
        };
        restoreWindow.ShowDialog();
        Log($"Restore dialog closed for: {dlg.FileName}");
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSkyrim()) return;
        try
        {
            var root = SkyrimPathBox.Text;
            var cccPath = Path.Combine(root, "Skyrim.ccc");
            var aeList = AEContentDetector.ReadCccList(root);
            var present = AEContentDetector.FindCategorizedFiles(root);
            var missing = AEContentDetector.FindMissingAEPlugins(root);

            Log("--- Validation Report ---");
            if (!File.Exists(cccPath))
            {
                Log("WARN  Skyrim.ccc not found in game root. Cannot determine canonical AE list.");
                Log($"       Falling back to pattern matching. {present.Count} cc* file(s) detected:");
                foreach (var p in present) Log($"       [{p.Category}] {p.Name}");
                return;
            }

            Log($"Canonical AE list (Skyrim.ccc): {aeList.Count} plugin(s) expected");

            var aePresent = present.Where(p => p.Category == ContentCategory.AE).ToList();
            var ccPresent = present.Where(p => p.Category == ContentCategory.OtherCC).ToList();

            Log($"AE content present: {aePresent.Count} file(s)");
            foreach (var p in aePresent) Log($"    OK   {p.Name}");

            if (missing.Count > 0)
            {
                Log($"AE content MISSING: {missing.Count} plugin(s)");
                foreach (var m in missing) Log($"    MISS {m}");
            }
            else
            {
                Log("AE content: nothing missing.");
            }

            if (ccPresent.Count > 0)
            {
                Log($"Other CC (not in AE bundle): {ccPresent.Count} file(s)");
                foreach (var p in ccPresent) Log($"    EXTRA {p.Name}");
            }
            Log("--- End Report ---");
        }
        catch (Exception ex)
        {
            Log($"ERR  {ex.Message}");
        }
    }

    private bool ValidateSkyrim()
    {
        var p = SkyrimPathBox.Text;
        if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p))
        {
            MessageBox.Show("Please select a valid Skyrim folder first.",
                "No Skyrim folder", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        if (!Directory.Exists(Path.Combine(p, "Data")))
        {
            MessageBox.Show("The selected folder has no Data subfolder.",
                "Invalid folder", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        return true;
    }

    private bool ValidateBackupFolder()
    {
        var f = BackupFolderBox.Text;
        if (string.IsNullOrWhiteSpace(f))
        {
            MessageBox.Show("Please select a backup folder first.",
                "No backup folder", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        try
        {
            Directory.CreateDirectory(f);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not create backup folder:\n{ex.Message}",
                "Invalid backup folder", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        return true;
    }

    private void Log(string msg)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        LogScroller.ScrollToBottom();
    }
}
