using SkyrimAEBackup.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SkyrimAEBackup;

public partial class RestoreDialog : Window
{
    public class FileItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly string _zipPath;
    private readonly string _skyrimPath;
    private readonly ObservableCollection<FileItem> _items = new();
    private bool _isRestoring;

    public RestoreDialog(string zipPath, string skyrimPath)
    {
        InitializeComponent();
        _zipPath = zipPath;
        _skyrimPath = skyrimPath;
        ZipPathText.Text = zipPath;
        FileList.ItemsSource = _items;
        Loaded += (_, _) => LoadEntries();
    }

    private void LoadEntries()
    {
        try
        {
            var names = BackupManager.ListAEEntries(_zipPath);
            _items.Clear();
            foreach (var n in names)
                _items.Add(new FileItem { Name = n, IsSelected = true });
            UpdateCount();
            Log($"Found {names.Count} AE file(s) in backup.");
            if (names.Count == 0)
            {
                RestoreBtn.IsEnabled = false;
                StatusText.Text = "No AE content in this zip.";
            }
        }
        catch (Exception ex)
        {
            Log($"ERR  {ex.Message}");
            RestoreBtn.IsEnabled = false;
        }
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
        var selected = _items.Count(i => i.IsSelected);
        CountText.Text = $"{selected} of {_items.Count} selected";
        RestoreBtn.IsEnabled = selected > 0 && !_isRestoring;
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.IsSelected).Select(i => i.Name).ToList();
        if (selected.Count == 0) return;

        var confirm = MessageBox.Show(
            $"Restore {selected.Count} file(s) into your Skyrim Data folder, overwriting existing files?",
            "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        _isRestoring = true;
        SetControlsEnabled(false);
        Progress.Value = 0;
        Progress.Maximum = selected.Count;
        ProgressText.Text = $"0 / {selected.Count}";
        StatusText.Text = "Restoring...";

        var zip = _zipPath;
        var skyrim = _skyrimPath;

        try
        {
            var progress = new Progress<RestoreProgress>(p =>
            {
                Progress.Maximum = p.Total > 0 ? p.Total : 1;
                Progress.Value = p.Current;
                ProgressText.Text = $"{p.Current} / {p.Total}";
                Log(p.Message);
            });

            int restored = await Task.Run(() =>
                BackupManager.RestoreSelectedEntries(zip, skyrim, selected, progress));

            StatusText.Text = $"Restored {restored} file(s) successfully.";
            CancelBtn.Content = "Close";
        }
        catch (Exception ex)
        {
            Log($"ERR  {ex.Message}");
            StatusText.Text = "Restore failed.";
            _isRestoring = false;
            SetControlsEnabled(true);
        }
        _isRestoring = false;
        // Keep Restore button disabled after success — restoring twice is unusual
        RestoreBtn.IsEnabled = false;
    }

    private void SetControlsEnabled(bool enabled)
    {
        RestoreBtn.IsEnabled = enabled;
        FileList.IsEnabled = enabled;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Log(string msg)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        LogScroller.ScrollToBottom();
    }
}
