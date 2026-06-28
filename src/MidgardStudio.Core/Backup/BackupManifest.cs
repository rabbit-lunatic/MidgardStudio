using System.Text.Json;
using System.Text.Json.Serialization;

namespace MidgardStudio.Core.Backup;

/// <summary>One file in a snapshot: path relative to the snapshot root, size, and SHA-256 (uppercase hex).
/// <see cref="Sha256"/> is null for legacy (pre-dedup) snapshots, which means "unknown — can't prove equality".</summary>
public sealed record BackupFile(string Path, long Bytes, string? Sha256);

/// <summary>
/// Snapshot metadata persisted as <c>manifest.json</c>. Version 2 adds per-file SHA-256 (for dedup +
/// integrity + diff), the active Display Encoding + ruleset stamp, and a pinned flag. Version 1 (legacy)
/// manifests had <c>Files</c> as a bare string array and none of the new fields — they deserialize with
/// defaults (null hashes, codepage 0, not pinned) and stay fully restorable.
/// </summary>
public sealed class BackupManifest
{
    /// <summary>2 = dedup model. Defaults to 1 so a legacy manifest (no <c>Version</c> field) reads as
    /// legacy; <see cref="BackupManifest"/>s created by the current code set this to 2 explicitly.</summary>
    public int Version { get; set; } = 1;

    public DateTime TimestampUtc { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    /// <summary>Pinned snapshots are exempt from retention pruning.</summary>
    public bool Pinned { get; set; }

    /// <summary>The active Display Encoding codepage when the snapshot was taken (0 = unknown/legacy).</summary>
    public int EncodingCodepage { get; set; }

    /// <summary>The ruleset (re / pre-re) active at snapshot time (empty = unknown/legacy).</summary>
    public string Ruleset { get; set; } = string.Empty;

    [JsonConverter(typeof(BackupFilesConverter))]
    public List<BackupFile> Files { get; set; } = new();

    public long TotalBytes { get; set; }

    /// <summary>True when this snapshot predates the dedup model (no per-file hashes to plan/verify against).</summary>
    [JsonIgnore] public bool IsLegacy => Version < 2 || Files.Any(f => f.Sha256 is null);
}

/// <summary>Reads <c>Files</c> as EITHER a legacy bare string array (<c>["import/x.yml", …]</c> → path-only,
/// null hash) OR the v2 object array (<c>[{Path,Bytes,Sha256}, …]</c>); always writes the v2 object form.</summary>
public sealed class BackupFilesConverter : JsonConverter<List<BackupFile>>
{
    public override List<BackupFile> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<BackupFile>();
        if (reader.TokenType != JsonTokenType.StartArray) { reader.Skip(); return list; }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                list.Add(new BackupFile(reader.GetString() ?? string.Empty, 0, null)); // legacy: path only
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                string path = string.Empty; long bytes = 0; string? sha = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    string prop = reader.GetString() ?? string.Empty;
                    reader.Read();
                    switch (prop)
                    {
                        case "Path": path = reader.GetString() ?? string.Empty; break;
                        case "Bytes": bytes = reader.GetInt64(); break;
                        case "Sha256": sha = reader.TokenType == JsonTokenType.Null ? null : reader.GetString(); break;
                        default: reader.Skip(); break;
                    }
                }
                list.Add(new BackupFile(path, bytes, sha));
            }
            else { reader.Skip(); }
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<BackupFile> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var f in value)
        {
            writer.WriteStartObject();
            writer.WriteString("Path", f.Path);
            writer.WriteNumber("Bytes", f.Bytes);
            if (f.Sha256 is null) writer.WriteNull("Sha256"); else writer.WriteString("Sha256", f.Sha256);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }
}
