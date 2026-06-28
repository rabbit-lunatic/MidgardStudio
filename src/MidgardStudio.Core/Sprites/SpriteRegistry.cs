namespace MidgardStudio.Core.Sprites;

/// <summary>One queued (not-yet-saved) sprite registration: a constant name bound to an id, plus its
/// sprite file. The same shape covers a mob (<c>JT_X = mobId</c> + <c>JobNameTable</c>) and an accessory
/// (<c>ACCESSORY_X = id</c> + <c>AccNameTable</c>).</summary>
public sealed record PendingRegistration(string ConstantName, int Id, string Sprite);

/// <summary>
/// Pure registration math over the on-disk constants plus an in-memory pending queue. Lets the App sprite
/// services answer "next free id?" and "which ids are registered?" identically whether or not a
/// registration has been flushed to disk yet — the validator must see the working state (disk ∪ pending),
/// not just disk. Kept here so it is unit-testable without touching files.
/// </summary>
public static class SpriteRegistry
{
    /// <summary>Next free id, accounting for on-disk constants AND pending (unsaved) registrations, so two
    /// pending accessory links can't be handed the same id. Matches <c>AccessoryTables.NextFreeId</c>
    /// (returns 1 when nothing exists yet).</summary>
    public static int NextFreeId(IReadOnlyDictionary<string, int> diskConstants, IEnumerable<PendingRegistration> pending)
    {
        int max = 0;
        foreach (var v in diskConstants.Values) if (v > max) max = v;
        foreach (var p in pending) if (p.Id > max) max = p.Id;
        return max + 1;
    }

    /// <summary>All ids registered in the working state — on-disk constant values ∪ pending registrations.</summary>
    public static HashSet<int> RegisteredIds(IReadOnlyDictionary<string, int> diskConstants, IEnumerable<PendingRegistration> pending)
    {
        var set = new HashSet<int>(diskConstants.Values);
        foreach (var p in pending) set.Add(p.Id);
        return set;
    }

    /// <summary>True when a constant name already exists on disk or is already pending, so a flush doesn't
    /// append a duplicate constant.</summary>
    public static bool HasConstant(IReadOnlyDictionary<string, int> diskConstants, IEnumerable<PendingRegistration> pending, string constantName)
    {
        if (diskConstants.ContainsKey(constantName)) return true;
        foreach (var p in pending) if (p.ConstantName == constantName) return true;
        return false;
    }
}
