using System.IO;
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

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSkyrim()) return;
        if (!ValidateBackupFolder()) return;

        var skyrim = SkyrimPathBox.Text;
        var folder = BackupFolderBox.Text;
        var zipPath = Path.Combine(folder, $"Skyrim_AE_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        SetBusy(true, "Backing up...");
        await Task.Run(() =>
        {
            try
            {
                var progress = new Progress<string>(m => Dispatcher.Invoke(() => Log(m)));
                var count = BackupManager.BackupAEContent(skyrim, zipPath, progress);
                Dispatcher.Invoke(() => Log($"OK  Backup complete: {count} file(s) -> {zipPath}"));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Log($"ERR  {ex.Message}"));
            }
        });
        SetBusy(false, "Ready");
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSkyrim()) return;

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Zip files (*.zip)|*.zip",
            Title = "Select backup zip",
            InitialDirectory = Directory.Exists(BackupFolderBox.Text) ? BackupFolderBox.Text : ""
        };
        if (dlg.ShowDialog() != true) return;

        var confirm = MessageBox.Show(
            "Restore will extract AE content into your Skyrim Data folder, overwriting existing files. Continue?",
            "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        SetBusy(true, "Restoring...");
        var skyrim = SkyrimPathBox.Text;
        var zipPath = dlg.FileName;
        await Task.Run(() =>
        {
            try
            {
                var progress = new Progress<string>(m => Dispatcher.Invoke(() => Log(m)));
                var restored = BackupManager.RestoreAEContent(zipPath, skyrim, progress);
                Dispatcher.Invoke(() => Log($"OK  Restored {restored} file(s)."));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Log($"ERR  {ex.Message}"));
            }
        });
        SetBusy(false, "Ready");
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSkyrim()) return;
        try
        {
            var dataFolder = Path.Combine(SkyrimPathBox.Text, "Data");
            var files = AEContentDetector.FindAEFiles(dataFolder);
            Log($"Found {files.Count} AE/CC file(s) in Data folder:");
            foreach (var f in files) Log($"    {Path.GetFileName(f)}");
            if (files.Count == 0) Log("    (none)");
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

    private void SetBusy(bool busy, string status)
    {
        BackupBtn.IsEnabled = !busy;
        RestoreBtn.IsEnabled = !busy;
        ScanBtn.IsEnabled = !busy;
        StatusText.Text = status;
    }

    private void Log(string msg)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        LogScroller.ScrollToBottom();
    }
}
