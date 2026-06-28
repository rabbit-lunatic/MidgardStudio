using System;
using System.Collections.Generic;
using System.Text.Json;
using MidgardStudio.Core.Backup;

namespace MidgardStudio.Tests;

/// <summary>The pure backup logic (ADR-0003): dedup planner, diff, retention, and legacy-manifest back-compat.</summary>
public class BackupCoreTests
{
    private static BackupManifest Manifest(params (string Path, string? Sha)[] files)
    {
        var m = new BackupManifest();
        foreach (var (p, s) in files) m.Files.Add(new BackupFile(p, 0, s));
        return m;
    }

    // ---- planner ----

    [Fact]
    public void Plan_links_unchanged_copies_changed_and_new()
    {
        var prev = Manifest(("import/item_db.yml", "AAA"), ("client/itemInfo_C.lua", "BBB"));
        var current = new List<(string, long, string)>
        {
            ("import/item_db.yml", 10, "AAA"),       // unchanged -> link
            ("client/itemInfo_C.lua", 20, "CCC"),    // changed   -> copy
            ("skillinfoz/skillid.lub", 30, "DDD"),   // new       -> copy
        };
        var plan = BackupPlanner.Plan(current, prev);

        Assert.Equal(BackupFileAction.Link, plan.Find(p => p.Path == "import/item_db.yml")!.Action);
        Assert.Equal(BackupFileAction.Copy, plan.Find(p => p.Path == "client/itemInfo_C.lua")!.Action);
        Assert.Equal(BackupFileAction.Copy, plan.Find(p => p.Path == "skillinfoz/skillid.lub")!.Action);
    }

    [Fact]
    public void Plan_copies_everything_when_no_previous()
    {
        var plan = BackupPlanner.Plan(new List<(string, long, string)> { ("a", 1, "X") }, previous: null);
        Assert.Equal(BackupFileAction.Copy, plan[0].Action);
    }

    [Fact]
    public void Plan_copies_against_a_legacy_hashless_previous()
    {
        var prev = Manifest(("a", null)); // legacy snapshot: no hash to compare
        var plan = BackupPlanner.Plan(new List<(string, long, string)> { ("a", 1, "X") }, prev);
        Assert.Equal(BackupFileAction.Copy, plan[0].Action);
    }

    // ---- diff ----

    [Fact]
    public void Diff_classifies_added_modified_removed_unchanged()
    {
        var snapshot = Manifest(("a", "X"), ("b", "Y"), ("d", "V")); // d only in snapshot
        var current = new List<(string, string)> { ("a", "X"), ("b", "Z"), ("c", "W") }; // c only live
        var diff = BackupDiff.Compare(snapshot, current);

        Assert.Equal(BackupChangeKind.Unchanged, diff.Find(c => c.Path == "a")!.Kind);
        Assert.Equal(BackupChangeKind.Modified, diff.Find(c => c.Path == "b")!.Kind);
        Assert.Equal(BackupChangeKind.Added, diff.Find(c => c.Path == "d")!.Kind);   // restore would add d
        Assert.Equal(BackupChangeKind.Removed, diff.Find(c => c.Path == "c")!.Kind); // restore would drop c
    }

    [Fact]
    public void Diff_treats_legacy_null_hash_as_modified()
    {
        var snapshot = Manifest(("a", null));
        var diff = BackupDiff.Compare(snapshot, new List<(string, string)> { ("a", "X") });
        Assert.Equal(BackupChangeKind.Modified, diff[0].Kind);
    }

    // ---- retention ----

    [Fact]
    public void Retention_prunes_oldest_unpinned_beyond_keep()
    {
        var snaps = new List<SnapshotRef>
        {
            new("s1", new DateTime(2026, 1, 1), false),
            new("s2", new DateTime(2026, 1, 2), false),
            new("s3", new DateTime(2026, 1, 3), false),
            new("s4", new DateTime(2026, 1, 4), false),
        };
        var prune = RetentionPolicy.SelectForPrune(snaps, keep: 2);
        Assert.Equal(new[] { "s2", "s1" }, prune); // newest two (s4,s3) kept; oldest two pruned
    }

    [Fact]
    public void Retention_never_prunes_pinned()
    {
        var snaps = new List<SnapshotRef>
        {
            new("old-pinned", new DateTime(2026, 1, 1), true),
            new("s2", new DateTime(2026, 1, 2), false),
            new("s3", new DateTime(2026, 1, 3), false),
            new("s4", new DateTime(2026, 1, 4), false),
        };
        var prune = RetentionPolicy.SelectForPrune(snaps, keep: 2);
        Assert.DoesNotContain("old-pinned", prune);   // pinned kept regardless of age
        Assert.Equal(new[] { "s2" }, prune);          // keep-2 applies to unpinned (s4,s3 kept; s2 pruned)
    }

    // ---- manifest back-compat ----

    [Fact]
    public void Manifest_reads_legacy_string_array_files_as_hashless()
    {
        const string legacy = """
        { "TimestampUtc": "2026-01-01T00:00:00Z", "Label": "old", "Files": ["import/item_db.yml", "client/itemInfo_C.lua"], "TotalBytes": 123 }
        """;
        var m = JsonSerializer.Deserialize<BackupManifest>(legacy)!;
        Assert.Equal(2, m.Files.Count);
        Assert.All(m.Files, f => Assert.Null(f.Sha256));
        Assert.True(m.IsLegacy);
        Assert.Equal("import/item_db.yml", m.Files[0].Path);
    }

    [Fact]
    public void Manifest_round_trips_v2_object_files()
    {
        var m = new BackupManifest { Version = 2, EncodingCodepage = 1252, Ruleset = "re" };
        m.Files.Add(new BackupFile("import/item_db.yml", 42, "ABC123"));
        string json = JsonSerializer.Serialize(m);
        var back = JsonSerializer.Deserialize<BackupManifest>(json)!;
        Assert.False(back.IsLegacy);
        Assert.Equal("ABC123", back.Files[0].Sha256);
        Assert.Equal(42, back.Files[0].Bytes);
        Assert.Equal(1252, back.EncodingCodepage);
        Assert.Equal("re", back.Ruleset);
    }
}
