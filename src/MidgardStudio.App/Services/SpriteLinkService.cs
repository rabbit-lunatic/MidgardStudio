using System;
using System.IO;
using MidgardStudio.Core.Commands;
using MidgardStudio.Core.IO;
using MidgardStudio.Core.Lua;
using MidgardStudio.Core.Sprites;
using MidgardStudio.Core.Workspace;

namespace MidgardStudio.App.Services;

/// <summary>
/// Registers a headgear/accessory sprite: allocates an ACCESSORY_IDs constant, maps it to the sprite file
/// in accname.lub (+ accname_eng.lub), and reports the View id to set on the server item. Registrations are
/// <b>queued in memory</b> and written only on the next Save (so they undo/discard like every other edit),
/// via one atomic transaction. Mapped-view / id lookups reflect the working state (disk ∪ pending) so a
/// queued link reads as already mapped and two pending links can't be handed the same id.
/// </summary>
public sealed class SpriteLinkService : IDirtySource
{
    private readonly WorkspaceSession _session;
    private LuaFileCodec _codec => _session.ClientCodec; // fixed Windows-1252 (the RO client boundary), independent of the profile Display Encoding
    private readonly List<PendingRegistration> _pending = new();

    public SpriteLinkService(WorkspaceSession session)
    {
        _session = session;
        _session.WorkspaceReloaded += () => { if (_pending.Count > 0) { _pending.Clear(); RaiseDirty(); } };
    }

    private WorkspacePaths Paths => _session.Paths;

    private string DataInfoDir => Path.Combine(Paths.LuaFilesRoot, "datainfo");
    private string AccIdPath => Path.Combine(DataInfoDir, "accessoryid.lub");
    private string AccNamePath => Path.Combine(DataInfoDir, "accname.lub");
    private string AccNameEngPath => Path.Combine(DataInfoDir, "accname_eng.lub");

    public bool IsAvailable => File.Exists(AccIdPath) && File.Exists(AccNamePath);

    // ---- unsaved state: one dirty source among several (see CompositeDirtyState) ----
    public bool IsDirty => _pending.Count > 0;
    public event Action? DirtyChanged;
    private void RaiseDirty() => DirtyChanged?.Invoke();

    /// <summary>The file the next <see cref="Save"/> writes to (for the save summary).</summary>
    public string SaveTargetPath => AccIdPath;

    private Dictionary<string, int> DiskConstants() =>
        File.Exists(AccIdPath) ? AccessoryTables.ReadConstants(_codec.ReadText(AccIdPath)) : new();

    /// <summary>True when an ACCESSORY_IDs constant is mapped to this View id in the working state (disk ∪ pending).</summary>
    public bool HasView(int viewId) => MappedViewIds().Contains(viewId);

    /// <summary>All View ids mapped in the working state (accessoryid.lub ∪ pending). Tolerant of a malformed
    /// file on the read/validation path (returns just the pending ids); the Save path is fail-loud instead.</summary>
    public HashSet<int> MappedViewIds()
    {
        try { return SpriteRegistry.RegisteredIds(DiskConstants(), _pending); }
        catch { return SpriteRegistry.RegisteredIds(new Dictionary<string, int>(), _pending); }
    }

    /// <summary>The sprite file mapped to a View id (working state: pending first, else accessoryid+accname), or null.</summary>
    public string? SpriteForView(int viewId)
    {
        var pendingHit = _pending.FirstOrDefault(p => p.Id == viewId);
        if (pendingHit is not null) return pendingHit.Sprite;
        if (!IsAvailable) return null;
        try
        {
            var constants = DiskConstants();
            string? constName = constants.FirstOrDefault(kv => kv.Value == viewId).Key;
            if (constName is null) return null;
            var names = AccessoryTables.ReadNames(_codec.ReadText(AccNamePath), "AccNameTable");
            return names.GetValueOrDefault(constName);
        }
        catch { return null; }
    }

    /// <summary>Plans an accessory registration WITHOUT mutating state: allocates the ACCESSORY_IDs constant
    /// + id from the working state (disk ∪ pending) so two pending links can't collide. The caller commits it
    /// through the undo stack via <see cref="AddPending"/> / <see cref="RemovePending"/>.</summary>
    public PendingRegistration PlanAccessory(string aegisName, string spriteFile)
    {
        var disk = DiskConstants();
        string baseName = "ACCESSORY_" + Sanitize(aegisName);
        string constName = baseName;
        int suffix = 1;
        while (SpriteRegistry.HasConstant(disk, _pending, constName)) constName = $"{baseName}_{suffix++}";
        int id = SpriteRegistry.NextFreeId(disk, _pending);
        string sprite = spriteFile.StartsWith("_", StringComparison.Ordinal) ? spriteFile : "_" + spriteFile;
        return new PendingRegistration(constName, id, sprite);
    }

    public void AddPending(PendingRegistration p) { _pending.Add(p); RaiseDirty(); }

    public void RemovePending(PendingRegistration p) { if (_pending.Remove(p)) RaiseDirty(); }

    /// <summary>Flushes queued registrations to accessoryid.lub + accname.lub (+ accname_eng.lub) in one
    /// atomic transaction. The appends throw (fail-loud) on a malformed table before anything is staged.</summary>
    public void Save()
    {
        if (_pending.Count == 0) return;
        string idText = _codec.ReadText(AccIdPath);
        string nameText = _codec.ReadText(AccNamePath);
        bool hasEng = File.Exists(AccNameEngPath);
        string engText = hasEng ? _codec.ReadText(AccNameEngPath) : string.Empty;
        var have = new HashSet<string>(AccessoryTables.ReadConstants(idText).Keys, StringComparer.Ordinal);
        foreach (var p in _pending)
        {
            if (have.Add(p.ConstantName))
                idText = AccessoryTables.AppendConstant(idText, "ACCESSORY_IDs", p.ConstantName, p.Id);
            nameText = AccessoryTables.AppendName(nameText, "AccNameTable", "ACCESSORY_IDs", p.ConstantName, p.Sprite);
            if (hasEng)
                engText = AccessoryTables.AppendName(engText, "AccNameTable_Eng", "ACCESSORY_IDs", p.ConstantName, p.Sprite);
        }
        var tx = new FileTransaction(Path.Combine(Paths.LuaFilesRoot, ".midgard-backup"));
        tx.Stage(AccIdPath, _codec.EncodeText(idText));
        tx.Stage(AccNamePath, _codec.EncodeText(nameText));
        if (hasEng) tx.Stage(AccNameEngPath, _codec.EncodeText(engText));
        tx.Commit();
        _pending.Clear();
        RaiseDirty();
    }

    private static string Sanitize(string s) =>
        new(s.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_').ToArray());
}
