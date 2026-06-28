namespace MidgardStudio.Core.Backup;

/// <summary>How a file is written into a new snapshot.</summary>
public enum BackupFileAction
{
    /// <summary>Copy the live file into the snapshot (new or changed content).</summary>
    Copy,
    /// <summary>Hardlink to the previous snapshot's byte-identical copy (no extra disk).</summary>
    Link,
}

/// <summary>One file's plan for a new snapshot.</summary>
public sealed record BackupFilePlan(string Path, long Bytes, string Sha256, BackupFileAction Action);

/// <summary>
/// Decides, per file, whether a new snapshot should hardlink it to the previous snapshot's copy or copy it
/// fresh — purely from hashes, so it's testable without touching disk. A file is hardlinked only when the
/// previous snapshot has the SAME path with a non-null, byte-equal SHA-256 (so a legacy hash-less previous
/// snapshot forces a copy, and an encoding change — which alters bytes — forces a copy).
/// </summary>
public static class BackupPlanner
{
    public static List<BackupFilePlan> Plan(
        IReadOnlyList<(string Path, long Bytes, string Sha256)> current, BackupManifest? previous)
    {
        var prevByPath = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (previous is not null)
            foreach (var f in previous.Files)
                prevByPath[f.Path] = f.Sha256;

        var plans = new List<BackupFilePlan>(current.Count);
        foreach (var (path, bytes, sha) in current)
        {
            bool canLink = prevByPath.TryGetValue(path, out var prevSha) && prevSha is not null && prevSha == sha;
            plans.Add(new BackupFilePlan(path, bytes, sha, canLink ? BackupFileAction.Link : BackupFileAction.Copy));
        }
        return plans;
    }
}
