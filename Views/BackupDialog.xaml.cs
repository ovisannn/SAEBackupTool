using SkyrimAEBackup.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SkyrimAEBackup;

public partial class BackupDialog : Window
{
    public class FileItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public ContentCategory Category { get; set; }

        public string CategoryLabel => Category switch
        {
            ContentCategory.AE => "AE",
            ContentCategory.OtherCC => "CC",
            _ => "?"
        };

        public Brush BadgeBackground => Category switch
        {
            ContentCategory.AE => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),  // green
            ContentCategory.OtherCC => new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)), // blue
            _ => new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75))                       // gray
        };

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly string _skyrimPath;
    private readonly string _backupFolder;
    private readonly ObservableCollection<FileItem> _items = new();
    private bool _isBackingUp;

    public BackupDialog(string skyrimPath, string backupFolder)
    {
        InitializeComponent();
        _skyrimPath = skyrimPath;
        _backupFolder = backupFolder;
        FileList.ItemsSource = _items;
        Loaded += (_, _) => LoadFiles();
    }

    private void LoadFiles()
    {
        try
        {
            var found = AEContentDetector.FindCategorizedFiles(_skyrimPath);
            _items.Clear();
            foreach (var (path, name, cat) in found)
                _items.Add(new FileItem { Name = name, FullPath = path, Category = cat, IsSelected = true });

            var ae = found.Count(f => f.Category == ContentCategory.AE);
            var cc = found.Count(f => f.Category == ContentCategory.OtherCC);
            Log($"Found {found.Count} file(s): {ae} AE, {cc} other CC.");

            var cccPath = Path.Combine(_skyrimPath, "Skyrim.ccc");
            if (!File.Exists(cccPath))
                Log("Note: Skyrim.ccc not found — cannot reliably split AE vs CC. All matched files shown as CC.");

            UpdateCount();
            if (found.Count == 0)
            {
                BackupBtn.IsEnabled = false;
                StatusText.Text = "No AE/CC content found in Data folder.";
            }
        }
        catch (Exception ex)
        {
            Log($"ERR  {ex.Message}");
            BackupBtn.IsEnabled = false;
        }
    }

    private void SelectAllAE_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in _items) i.IsSelected = i.Category == ContentCategory.AE;
        UpdateCount();
    }

    private void SelectAllCC_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in _items) i.IsSelected = i.Category == ContentCategory.OtherCC;
        UpdateCount();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in _items) i.IsSelected = true;
        UpdateCount();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in _items) i.IsSelected = false;
        UpdateCount();
    }

    private void ItemCheck_Changed(object sender, RoutedEventArgs e) => UpdateCount();

    private void UpdateCount()
    {
        var sel = _items.Count(i => i.IsSelected);
        var ae = _items.Count(i => i.IsSelected && i.Category == ContentCategory.AE);
        var cc = _items.Count(i => i.IsSelected && i.Category == ContentCategory.OtherCC);
        CountText.Text = $"{sel} of {_items.Count} selected  ({ae} AE, {cc} CC)";
        BackupBtn.IsEnabled = sel > 0 && !_isBackingUp;
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.IsSelected).Select(i => i.FullPath).ToList();
        if (selected.Count == 0) return;

        try { Directory.CreateDirectory(_backupFolder); }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot use backup folder:\n{ex.Message}",
                "Backup folder error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var zipPath = Path.Combine(_backupFolder,
            $"Skyrim_AE_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        _isBackingUp = true;
        BackupBtn.IsEnabled = false;
        FileList.IsEnabled = false;
        Progress.Value = 0;
        Progress.Maximum = selected.Count;
        ProgressText.Text = $"0 / {selected.Count}";
        StatusText.Text = "Backing up...";

        try
        {
            var progress = new Progress<BackupProgress>(p =>
            {
                Progress.Maximum = p.Total > 0 ? p.Total : 1;
                Progress.Value = p.Current;
                ProgressText.Text = $"{p.Current} / {p.Total}";
                Log(p.Message);
            });

            int count = await Task.Run(() =>
                BackupManager.BackupSelectedFiles(selected, zipPath, progress));

            StatusText.Text = $"Backup complete: {count} file(s) saved.";
            Log($"OK  Saved to: {zipPath}");
            CancelBtn.Content = "Close";
        }
        catch (Exception ex)
        {
            Log($"ERR  {ex.Message}");
            StatusText.Text = "Backup failed.";
            _isBackingUp = false;
            FileList.IsEnabled = true;
            UpdateCount();
            return;
        }
        _isBackingUp = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Log(string msg)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        LogScroller.ScrollToBottom();
    }
}
