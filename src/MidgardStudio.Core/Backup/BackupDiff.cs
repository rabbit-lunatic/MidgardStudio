namespace MidgardStudio.Core.Backup;

/// <summary>How a snapshot file relates to the current live state (from the snapshot's perspective — i.e.
/// what restoring the snapshot would do).</summary>
public enum BackupChangeKind
{
    /// <summary>In the snapshot, not live — restore would create it.</summary>
    Added,
    /// <summary>In the snapshot and live with the same hash — restore is a no-op for it.</summary>
    Unchanged,
    /// <summary>In both but bytes differ (or the snapshot hash is unknown) — restore would overwrite it.</summary>
    Modified,
    /// <summary>Live, not in the snapshot — restore would drop it (for removable areas like import/).</summary>
    Removed,
}

public sealed record BackupChange(string Path, BackupChangeKind Kind);

/// <summary>
/// Pure file-level diff between a snapshot manifest and the current files (path + SHA-256). Powers both the
/// restore preview (snapshot vs live) and "what changed" (snapshot vs previous snapshot). Snapshot files
/// with a null hash (legacy) are reported <see cref="BackupChangeKind.Modified"/> — equality can't be proven.
/// </summary>
public static class BackupDiff
{
    public static List<BackupChange> Compare(BackupManifest snapshot, IReadOnlyList<(string Path, string Sha256)> current)
    {
        var cur = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (p, s) in current) cur[p] = s;

        var result = new List<BackupChange>();
        var snapPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in snapshot.Files)
        {
            snapPaths.Add(f.Path);
            if (!cur.TryGetValue(f.Path, out var curSha))
                result.Add(new BackupChange(f.Path, BackupChangeKind.Added));
            else if (f.Sha256 is not null && f.Sha256 == curSha)
                result.Add(new BackupChange(f.Path, BackupChangeKind.Unchanged));
            else
                result.Add(new BackupChange(f.Path, BackupChangeKind.Modified));
        }
        foreach (var (p, _) in current)
            if (!snapPaths.Contains(p))
                result.Add(new BackupChange(p, BackupChangeKind.Removed));
        return result;
    }
}
