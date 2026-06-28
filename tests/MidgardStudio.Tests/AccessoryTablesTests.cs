using System.IO;
using MidgardStudio.Core.Lua;
using MidgardStudio.Core.Workspace;

namespace MidgardStudio.Tests;

public class AccessoryTablesTests
{
    private static string DataInfo =>
        Path.Combine(WorkspaceConfigService.DefaultRepoRoot, "lua-files", "datainfo");

    [Fact]
    public void Reads_real_accessory_constants_and_names()
    {
        string idPath = Path.Combine(DataInfo, "accessoryid.lub");
        string namePath = Path.Combine(DataInfo, "accname.lub");
        if (!File.Exists(idPath) || !File.Exists(namePath)) return;

        var codec = new LuaFileCodec(1252);
        var constants = AccessoryTables.ReadConstants(codec.ReadText(idPath));
        Assert.True(constants.Count > 100, $"expected many accessory constants, got {constants.Count}");

        var names = AccessoryTables.ReadNames(codec.ReadText(namePath));
        Assert.True(names.Count > 50, $"expected many accname mappings, got {names.Count}");
    }

    [Fact]
    public void Append_constant_inserts_and_reparses()
    {
        string idPath = Path.Combine(DataInfo, "accessoryid.lub");
        if (!File.Exists(idPath)) return;

        var codec = new LuaFileCodec(1252);
        string text = codec.ReadText(idPath);
        var before = AccessoryTables.ReadConstants(text);
        int nextId = AccessoryTables.NextFreeId(before);

        string updated = AccessoryTables.AppendConstant(text, "ACCESSORY_IDs", "ACCESSORY_TEST_MIDGARD", nextId);
        var after = AccessoryTables.ReadConstants(updated);

        Assert.Equal(before.Count + 1, after.Count);
        Assert.Equal(nextId, after["ACCESSORY_TEST_MIDGARD"]);
    }

    [Fact]
    public void Append_constant_throws_when_table_missing()
    {
        // The target table isn't in the file — must fail loud (edit kept, file untouched), not silently no-op.
        Assert.Throws<InvalidDataException>(() =>
            AccessoryTables.AppendConstant("local other = {}\n", "SKID", "CUSTOM_1", 6608));
    }

    [Fact]
    public void Append_name_throws_when_table_missing()
    {
        Assert.Throws<InvalidDataException>(() =>
            AccessoryTables.AppendName("local other = {}\n", "AccNameTable", "ACCESSORY_IDs", "CUSTOM_1", "_custom"));
    }

    [Fact]
    public void Append_constant_throws_when_brace_unbalanced()
    {
        // Table opens but never closes — a malformed file. Must throw, not corrupt or silently drop.
        Assert.Throws<InvalidDataException>(() =>
            AccessoryTables.AppendConstant("SKID = {\n\tAT_FOO = 1,\n", "SKID", "CUSTOM_1", 6608));
    }
}
