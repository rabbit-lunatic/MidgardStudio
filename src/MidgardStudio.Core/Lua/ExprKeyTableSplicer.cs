using System.Text;

namespace MidgardStudio.Core.Lua;

/// <summary>
/// In-place splice writer for expression-keyed Lua tables (<c>SKILL_INFO_LIST = { [SKID.X] = { ... } }</c>).
/// Each supplied entry replaces its existing <c>[SKID.X] = { ... }</c> block; new keys are inserted before
/// the table's closing brace; every other byte (header comments, untouched entries) is preserved. The
/// expression-key sibling of <see cref="UnifiedItemInfoWriter"/> — the integer-keyed splicer can't address
/// <c>[SKID.X]</c> keys. Keys are matched EXACTLY (so <c>SKID.SU_LOPE</c> can't hit <c>SKID.SU_LOPED</c>).
/// </summary>
public static class ExprKeyTableSplicer
{
    /// <summary>Splices the given <c>(exprKey, block)</c> entries into <paramref name="tableName"/> in
    /// <paramref name="original"/>. <c>exprKey</c> is the full bracket text (e.g. <c>SKID.SM_BASH</c>);
    /// <c>block</c> is the formatted <c>\t[SKID.X] = { ... },\n</c> text.</summary>
    public static string Splice(string original, string tableName, IEnumerable<(string ExprKey, string Block)> entries)
    {
        var list = entries.ToList();
        if (list.Count == 0) return original;

        int tableOpen = LuaScan.FindTableOpen(original, tableName);
        if (tableOpen < 0)
        {
            // No table present — append a fresh one rather than corrupt the file.
            var fresh = new StringBuilder(original.TrimEnd());
            fresh.Append("\n\n").Append(tableName).Append(" = {\n");
            foreach (var (_, block) in list) fresh.Append(block);
            fresh.Append("}\n");
            return fresh.ToString();
        }

        var (blocks, tableClose) = LuaScan.ScanExprKeyTables(original, tableOpen);
        if (tableClose < 0)
            throw new InvalidDataException(
                $"Couldn't find the end of the '{tableName}' table in the client skill file, so your edit was NOT saved and the file was left untouched. " +
                "The file may have a mismatched brace — open it and check, then save again.");

        // Replace existing entries in one forward pass over the original (ascending offset), copying the
        // untouched gaps — O(file size) total instead of re-allocating the whole file once per replaced entry.
        // Entries never overlap, so the output is identical to the old replace-each pass.
        var hits = list.Where(e => blocks.ContainsKey(e.ExprKey))
            .Select(e => (Pos: blocks[e.ExprKey], e.Block))
            .OrderBy(x => x.Pos.BracketStart)
            .ToList();

        string text;
        if (hits.Count == 0)
        {
            text = original;
        }
        else
        {
            var sb = new StringBuilder(original.Length);
            int cursor = 0;
            foreach (var (pos, block) in hits)
            {
                int start = pos.BracketStart;
                while (start > 0 && (original[start - 1] == '\t' || original[start - 1] == ' ')) start--; // include line indent
                int commaEnd = pos.ValueClose + 1;
                if (commaEnd < original.Length && original[commaEnd] == ',') commaEnd++;
                sb.Append(original, cursor, start - cursor);
                sb.Append(block.TrimEnd('\n'));
                cursor = commaEnd;
            }
            sb.Append(original, cursor, original.Length - cursor);
            text = sb.ToString();
        }

        // Insert new entries just before the table's closing brace.
        var news = list.Where(e => !blocks.ContainsKey(e.ExprKey)).ToList();
        if (news.Count > 0)
        {
            int open2 = LuaScan.FindTableOpen(text, tableName);
            int close2 = LuaScan.FindMatchingBrace(text, open2);
            if (close2 < 0)
                throw new InvalidDataException(
                    $"Couldn't find the closing brace of the '{tableName}' table to insert new entries, so your edit was NOT saved and the file was left untouched. " +
                    "The file may have a mismatched brace — open it and check, then save again.");

            int p = close2 - 1;
            while (p > open2 && char.IsWhiteSpace(text[p])) p--;
            string sep = (text[p] == '{' || text[p] == ',') ? string.Empty : ",";

            var sb = new StringBuilder();
            sb.Append(sep).Append('\n');
            foreach (var (_, block) in news) sb.Append(block);
            text = text.Substring(0, close2) + sb + text.Substring(close2);
        }

        return text;
    }
}
