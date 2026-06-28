using System.Collections.Generic;
using MidgardStudio.Core.Sprites;

namespace MidgardStudio.Tests;

/// <summary>The pure registration math (disk ∪ pending) used by the deferred sprite-registration services.</summary>
public class SpriteRegistryTests
{
    private static Dictionary<string, int> Disk(params (string Name, int Id)[] entries)
    {
        var d = new Dictionary<string, int>(System.StringComparer.Ordinal);
        foreach (var (n, i) in entries) d[n] = i;
        return d;
    }

    [Fact]
    public void NextFreeId_is_one_when_nothing_exists() =>
        Assert.Equal(1, SpriteRegistry.NextFreeId(Disk(), new List<PendingRegistration>()));

    [Fact]
    public void NextFreeId_follows_the_highest_disk_value() =>
        Assert.Equal(6, SpriteRegistry.NextFreeId(Disk(("A", 5), ("B", 3)), new List<PendingRegistration>()));

    [Fact]
    public void NextFreeId_accounts_for_pending_so_two_links_dont_collide()
    {
        var disk = Disk(("A", 5));
        var pending = new List<PendingRegistration> { new("B", 6, "_b") };
        Assert.Equal(7, SpriteRegistry.NextFreeId(disk, pending));
    }

    [Fact]
    public void NextFreeId_uses_pending_even_when_disk_is_empty() =>
        Assert.Equal(4, SpriteRegistry.NextFreeId(Disk(), new List<PendingRegistration> { new("X", 3, "_x") }));

    [Fact]
    public void RegisteredIds_is_disk_union_pending()
    {
        var ids = SpriteRegistry.RegisteredIds(Disk(("A", 5), ("B", 6)), new List<PendingRegistration> { new("C", 9, "_c") });
        Assert.Equal(new HashSet<int> { 5, 6, 9 }, ids);
    }

    [Fact]
    public void HasConstant_sees_both_disk_and_pending()
    {
        var disk = Disk(("ACCESSORY_HAT", 1));
        var pending = new List<PendingRegistration> { new("ACCESSORY_CAPE", 2, "_cape") };
        Assert.True(SpriteRegistry.HasConstant(disk, pending, "ACCESSORY_HAT"));
        Assert.True(SpriteRegistry.HasConstant(disk, pending, "ACCESSORY_CAPE"));
        Assert.False(SpriteRegistry.HasConstant(disk, pending, "ACCESSORY_NEW"));
    }
}
