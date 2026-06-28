namespace MidgardStudio.Core.Lua;

/// <summary>
/// Reads and appends to the headgear sprite tables: accessoryid.lub (ACCESSORY_IDs constants) and
/// accname.lub (AccNameTable mapping constant -&gt; sprite file). Robe tables share the same shape.
/// </summary>
public static class AccessoryTables
{
    /// <summary>Parses ACCESSORY_IDs: constant name -> numeric id.</summary>
    public static Dictionary<string, int> ReadConstants(string accessoryIdText, string tableName = "ACCESSORY_IDs")
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var table = new LuaTableParser(accessoryIdText).ParseNamedTable(tableName);
        if (table is null) return result;
        foreach (var (name, value) in table.NameKeys)
            if (value is double d) result[name] = (int)d;
        return result;
    }

    /// <summary>Parses AccNameTable: constant name (from [ACCESSORY_IDs.X]) -> sprite file name.</summary>
    public static Dictionary<string, string> ReadNames(string accNameText, string tableName = "AccNameTable")
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var table = new LuaTableParser(accNameText).ParseNamedTable(tableName);
        if (table is null) return result;
        foreach (var (key, value) in table.ExprKeys)
        {
            int dot = key.LastIndexOf('.');
            string name = dot >= 0 ? key[(dot + 1)..] : key;
            if (value is string s) result[name] = s;
        }
        return result;
    }

    public static int NextFreeId(IReadOnlyDictionary<string, int> constants) =>
        constants.Count == 0 ? 1 : constants.Values.Max() + 1;

    /// <summary>Appends a constant <c>NAME = id,</c> inside the named table.</summary>
    public static string AppendConstant(string text, string tableName, string constantName, int id) =>
        InsertBeforeTableClose(text, tableName, $"\t{constantName} = {id},");

    /// <summary>Appends a <c>[ACCESSORY_IDs.NAME] = "sprite",</c> mapping inside the named table.</summary>
    public static string AppendName(string text, string tableName, string idsTableName, string constantName, string sprite) =>
        InsertBeforeTableClose(text, tableName, $"\t[{idsTableName}.{constantName}] = \"{sprite}\",");

    private static string InsertBeforeTableClose(string text, string tableName, string line)
    {
        // Reuse the shared string- AND comment-aware scanner so a brace inside a Lua comment can't
        // mis-locate the table's open/close (the old local scanners ignored comments).
        // These tables (SKID / ACCESSORY_IDs / AccNameTable / npc tables) are always present in a valid
        // base file, so a missing table means a malformed/incompatible file. Fail LOUD — the save path
        // keeps the edit in memory and rolls the transaction back — rather than silently returning the
        // file unchanged (which committed clean while dropping the edit, and could orphan references).
        int open = LuaScan.FindTableOpen(text, tableName);
        if (open < 0)
            throw new InvalidDataException(
                $"Couldn't find the '{tableName}' table in the client Lua file, so your change was NOT saved and the file was left untouched. " +
                "The file may be malformed or missing that table — open it and check, then save again.");

        int close = LuaScan.FindMatchingBrace(text, open);
        if (close < 0)
            throw new InvalidDataException(
                $"Couldn't find the end of the '{tableName}' table in the client Lua file, so your change was NOT saved and the file was left untouched. " +
                "The file may have a mismatched brace — open it and check, then save again.");

        // Add a field separator if the last entry lacks one — see LuaScan.SeparatorBeforeNewEntry (the rule
        // that bit v1.0.1). Place it right after the last value, then the new line just before the close brace.
        string sep = LuaScan.SeparatorBeforeNewEntry(text, open, close);
        int p = close - 1;
        while (p > open && char.IsWhiteSpace(text[p])) p--;
        string nl = text.Contains("\r\n") ? "\r\n" : "\n";
        return text[..(p + 1)] + sep + text[(p + 1)..close] + line + nl + text[close..];
    }
}
