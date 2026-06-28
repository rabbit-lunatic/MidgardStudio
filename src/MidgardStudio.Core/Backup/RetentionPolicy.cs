namespace MidgardStudio.Core.Backup;

/// <summary>Minimal snapshot identity for retention decisions.</summary>
public sealed record SnapshotRef(string Id, DateTime TimestampUtc, bool Pinned);

/// <summary>
/// Pure retention math: keep the newest <c>keep</c> UNPINNED snapshots plus ALL pinned ones; return the ids
/// of the unpinned snapshots beyond the cap (oldest first) to prune. Pinned snapshots are never returned.
/// </summary>
public static class RetentionPolicy
{
    public static List<string> SelectForPrune(IReadOnlyList<SnapshotRef> snapshots, int keep)
    {
        keep = Math.Max(1, keep);
        return snapshots
            .Where(s => !s.Pinned)
            .OrderByDescending(s => s.TimestampUtc)
            .Skip(keep)
            .Select(s => s.Id)
            .ToList();
    }
}
