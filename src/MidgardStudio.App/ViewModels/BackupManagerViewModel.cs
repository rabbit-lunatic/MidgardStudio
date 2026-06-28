using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MidgardStudio.App.Services;
using MidgardStudio.Core.Backup;

namespace MidgardStudio.App.ViewModels;

/// <summary>One file in the snapshot details list: path, its area group, and whether it changed in this snapshot.</summary>
public sealed record BackupFileRow(string Path, string Area, bool Changed);

/// <summary>
/// Tools ▸ Backup Manager. Lists the dated snapshots for the active profile and lets the user create a manual
/// backup, restore (with a what-changes preview), pin (exempt from retention), verify integrity, export/import
/// a snapshot as a .zip, reveal it on disk, or delete it. A restore reloads the workspace via the callback.
/// </summary>
public sealed partial class BackupManagerViewModel : ObservableObject
{
    private readonly BackupService _backups;
    private readonly Action _reloadAfterRestore;

    public BackupManagerViewModel(BackupService backups, Action reloadAfterRestore)
    {
        _backups = backups;
        _reloadAfterRestore = reloadAfterRestore;
        System.Windows.Data.CollectionViewSource.GetDefaultView(SelectedFiles)
            .GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(BackupFileRow.Area)));
        Refresh();
    }

    public ObservableCollection<BackupEntry> Items { get; } = new();

    /// <summary>The selected snapshot's files, grouped by area with a changed/carried flag (rebuilt on selection).</summary>
    public ObservableCollection<BackupFileRow> SelectedFiles { get; } = new();

    [ObservableProperty]
    private BackupEntry? _selected;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool HasSelection => Selected is not null;

    public bool IsEmpty => Items.Count == 0;

    public string PinButtonText => Selected?.Pinned == true ? "Unpin" : "Pin";

    public string? EncodingChip => Selected is null
        ? null
        : "Encoding " + BackupService.EncodingLabel(Selected.Manifest.EncodingCodepage)
          + (string.IsNullOrEmpty(Selected.Manifest.Ruleset) ? string.Empty : "  ·  " + Selected.Manifest.Ruleset);

    partial void OnSelectedChanged(BackupEntry? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(PinButtonText));
        OnPropertyChanged(nameof(EncodingChip));
        RebuildSelectedFiles();
    }

    private void RebuildSelectedFiles()
    {
        SelectedFiles.Clear();
        if (Selected is null) return;
        var changed = _backups.ChangedSincePrevious(Selected);
        foreach (var f in Selected.Manifest.Files.OrderBy(f => f.Path, StringComparer.Ordinal))
            SelectedFiles.Add(new BackupFileRow(f.Path, AreaOf(f.Path), changed.TryGetValue(f.Path, out var c) && c));
    }

    private static string AreaOf(string path) =>
        path.StartsWith("import/", StringComparison.Ordinal) ? "Server items"
        : path.StartsWith("SystemEN/", StringComparison.Ordinal) || path.StartsWith("client/", StringComparison.Ordinal) ? "Client items"
        : path.StartsWith("skillinfoz/", StringComparison.Ordinal) ? "Skills"
        : path.StartsWith("datainfo/", StringComparison.Ordinal) ? "Sprites"
        : "Other";

    [RelayCommand]
    private void Refresh()
    {
        var keep = Selected?.FolderPath;
        Items.Clear();
        // List() is newest-first; a stable OrderByDescending(Pinned) floats pinned to the top, newest-first within each group.
        foreach (var entry in _backups.List().OrderByDescending(e => e.Pinned)) Items.Add(entry);
        Selected = Items.FirstOrDefault(e => e.FolderPath == keep) ?? Items.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void CreateNow()
    {
        var entry = _backups.CreateBackup("Manual backup", "Created from the Backup Manager.");
        Refresh();
        if (entry is not null)
        {
            Selected = Items.FirstOrDefault(e => e.FolderPath == entry.FolderPath) ?? Selected;
            StatusMessage = $"Backup created — {entry.SummaryText}.";
        }
        else
        {
            StatusMessage = "Nothing to back up (no editable files found).";
        }
    }

    [RelayCommand]
    private void TogglePin(BackupEntry? entry)
    {
        entry ??= Selected;
        if (entry is null) return;
        _backups.SetPinned(entry, !entry.Pinned);
        bool nowPinned = entry.Pinned;
        Refresh();
        StatusMessage = nowPinned ? "Pinned — this backup is kept regardless of retention." : "Unpinned.";
    }

    [RelayCommand]
    private void Restore(BackupEntry? entry)
    {
        entry ??= Selected;
        if (entry is null) return;

        string preview;
        try
        {
            var diff = _backups.PreviewRestore(entry);
            int overwrite = diff.Count(c => c.Kind == BackupChangeKind.Modified);
            int add = diff.Count(c => c.Kind == BackupChangeKind.Added);
            int remove = diff.Count(c => c.Kind == BackupChangeKind.Removed);
            preview = overwrite + add + remove == 0
                ? "No changes — your current files already match this snapshot."
                : $"This will overwrite {overwrite}, add {add}, and remove {remove} file(s).";
        }
        catch
        {
            preview = "This replaces your current import data + client files with this snapshot.";
        }

        string encodingNote = _backups.EncodingDiffers(entry)
            ? $"\n\nNote: this snapshot was taken under {BackupService.EncodingLabel(entry.Manifest.EncodingCodepage)}; " +
              $"you're on {BackupService.EncodingLabel(_backups.CurrentCodepage)}. Restored bytes are exact, but legacy text may display differently."
            : string.Empty;

        if (!Views.ConfirmDialog.Show("Restore backup",
                $"Restore the backup from {entry.WhenText}?\n\n\"{entry.Label}\"\n\n{preview}\n\n" +
                "A safety backup of the current state is taken first." + encodingNote,
                yes: "Restore"))
            return;

        try
        {
            _backups.Restore(entry);
            _reloadAfterRestore();
            Refresh();
            StatusMessage = $"Restored backup from {entry.WhenText}. A safety backup of the previous state was saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Restore failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Verify(BackupEntry? entry)
    {
        entry ??= Selected;
        if (entry is null) return;
        var (ok, mismatch, unknown, missing) = _backups.Verify(entry);
        StatusMessage = mismatch == 0 && missing == 0
            ? $"Integrity OK — {ok} file(s) verified" + (unknown > 0 ? $" ({unknown} legacy, no checksum)." : ".")
            : $"⚠ Integrity problem — {mismatch} changed, {missing} missing of {ok + mismatch + missing} checksummed file(s).";
    }

    [RelayCommand]
    private void Export(BackupEntry? entry)
    {
        entry ??= Selected;
        if (entry is null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export backup",
            Filter = "Zip archive (*.zip)|*.zip",
            FileName = $"midgard-backup-{entry.When:yyyyMMdd-HHmmss}.zip",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _backups.Export(entry, dlg.FileName);
            StatusMessage = $"Exported to {dlg.FileName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Export failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Import()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Import backup", Filter = "Zip archive (*.zip)|*.zip" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var entry = _backups.Import(dlg.FileName);
            Refresh();
            Selected = Items.FirstOrDefault(e => e.FolderPath == entry.FolderPath) ?? Selected;
            StatusMessage = $"Imported backup — {entry.SummaryText}.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Import failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Delete(BackupEntry? entry)
    {
        entry ??= Selected;
        if (entry is null) return;

        if (!Views.ConfirmDialog.Show("Delete backup",
                $"Delete the backup from {entry.WhenText} permanently?", yes: "Delete")) return;

        _backups.Delete(entry);
        Refresh();
        StatusMessage = "Backup deleted.";
    }

    [RelayCommand]
    private void OpenFolder(BackupEntry? entry)
    {
        string path = entry?.FolderPath ?? _backups.RootDir;
        try
        {
            if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch { /* shell-open failure is non-fatal */ }
    }
}
