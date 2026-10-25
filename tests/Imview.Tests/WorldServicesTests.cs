using Imview.Core.Common;
using Imview.Core.Data;
using Imview.Core.Database.Models;
using Imview.Core.Database.Services;
using Imview.Core.Models;
using Imview.Core.Services;
using Xunit;

namespace Imview.Tests;

[Collection(WorldStoreProviderCollection.Name)]
public sealed class WorldServicesTests : IDisposable {

    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "spiraldb");

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "imview-tests-" + Guid.NewGuid().ToString("N"));

    private string DataRoot => Path.Combine(_tempRoot, "spiraldb");

    public WorldServicesTests() {
        foreach (var directory in new[] { "DropTables", "CreatureSpellbook", "NpcInventory", "NpcSpellInventory" }) {
            var target = Path.Combine(DataRoot, directory);
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(Path.Combine(FixtureRoot, directory), "*.json")) {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }
        }
        ConfigurationManager.Initialize(Path.Combine(_tempRoot, "config.ini"));
        ConfigurationManager.SetSetting(WorldStoreProvider.DataPackPathSetting, DataRoot);
        WorldStoreProvider.Reload();
    }

    public void Dispose() {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DropTable_Save_Get_Delete_RoundTrip() {
        var service = new DropTableService();

        var created = await service.CreateDropTableAsync("TEST-NEW", "first");
        Assert.Equal("TEST-NEW", created);
        var table = await service.GetDropTableAsync("TEST-NEW");
        Assert.NotNull(table);
        table.Description = "edited";
        Assert.True(await service.UpdateDropTableAsync(table));

        var fresh = await LoadFreshStoreAsync();
        Assert.Equal("edited", (await fresh.GetAsync<DropTable>("TEST-NEW"))?.Description);
        Assert.Contains("droptables_TEST-NEW.json", FilesIn("DropTables"));

        Assert.True(await service.DeleteDropTableAsync("droptables/test-new"));
        Assert.Null(await service.GetDropTableAsync("TEST-NEW"));
        Assert.DoesNotContain("droptables_TEST-NEW.json", FilesIn("DropTables"));
        Assert.NotNull(await service.GetDropTableAsync("droptables/test"));
    }

    [Fact]
    public async Task DropTable_Renamed_OldFileRemoved() {
        var service = new DropTableService();
        var table = await service.GetDropTableAsync("TEST");
        table!.Name = "TEST-RENAMED";

        Assert.True(await service.UpdateDropTableAsync(table, previousName: "TEST"));

        Assert.Equal(["droptables_TEST-RENAMED.json"], FilesIn("DropTables"));
        Assert.Null(await service.GetDropTableAsync("TEST"));
    }

    [Fact]
    public async Task DropTable_RenameToExistingName_Refused() {
        var service = new DropTableService();
        await service.CreateDropTableAsync("TEST-OTHER", "keep me");
        var table = await service.GetDropTableAsync("TEST");
        table!.Name = "TEST-OTHER";

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateDropTableAsync(table, previousName: "TEST"));

        Assert.Equal("keep me", (await service.GetDropTableAsync("TEST-OTHER"))?.Description);
        Assert.NotNull(await service.GetDropTableAsync("TEST"));
        Assert.Equal(["droptables_TEST-OTHER.json", "droptables_test.json"], FilesIn("DropTables").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task DropTable_EditsDoNotReachStoreUntilSaved() {
        var service = new DropTableService();
        var table = await service.GetDropTableAsync("TEST");
        Assert.NotNull(table);
        var itemCount = table.Items.Count;

        table.Description = "unsaved";
        table.Items.Add(new DropItem { ItemId = "1" });

        var again = await service.GetDropTableAsync("TEST");
        Assert.Equal("", again?.Description);
        Assert.Equal(itemCount, again!.Items.Count);
        Assert.Equal("", (await service.GetAllDropTablesAsync()).Single().Description);

        Assert.True(await service.UpdateDropTableAsync(table));
        table.Description = "after save";

        Assert.Equal("unsaved", (await service.GetDropTableAsync("TEST"))?.Description);
        var fresh = await LoadFreshStoreAsync();
        Assert.Equal("unsaved", (await fresh.GetAsync<DropTable>("TEST"))?.Description);
        Assert.Equal(["droptables_test.json"], FilesIn("DropTables"));
    }

    [Fact]
    public async Task DropTable_Search_ByName() {
        var service = new DropTableService();
        await service.CreateDropTableAsync("Goblin Loot");
        await service.CreateDropTableAsync("Troll Loot");

        var found = await service.SearchDropTablesAsync("goblin");

        Assert.Equal(["Goblin Loot"], found.Select(t => t.Name));
        Assert.Equal(["Goblin Loot", "TEST", "Troll Loot"], (await service.GetAllDropTablesAsync()).Select(t => t.Name));
    }

    [Fact]
    public async Task CreatureDeck_Save_Has_AllNames() {
        Assert.True(await CreatureDeckService.SaveCreatureDeckAsync(new CreatureDeck { DeckName = "Deck-New", SpellTemplateIds = [1, 2] }));
        Assert.True(await CreatureDeckService.SaveCreatureDeckAsync(new CreatureDeck { DeckName = "Mdeck-Mix-R1-1", SpellTemplateIds = [7] }));

        Assert.True(await CreatureDeckService.HasCreatureDeckAsync("Deck-New"));
        Assert.False(await CreatureDeckService.HasCreatureDeckAsync("Deck-Missing"));
        Assert.Equal(["Deck-New", "Mdeck-Mix-R1-1"], (await CreatureDeckService.GetAllDeckNamesAsync()).Order());
        var fresh = await LoadFreshStoreAsync();
        Assert.Equal([1L, 2L], (await fresh.GetAsync<CreatureSpellbook>("Deck-New"))?.SpellTemplateIds);
        Assert.Equal([7L], (await fresh.GetAsync<CreatureSpellbook>("Mdeck-Mix-R1-1"))?.SpellTemplateIds);
        Assert.Equal(["CreatureSpellbooks_33-A.json", "creaturespellbook_Deck-New.json"], FilesIn("CreatureSpellbook").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task NpcInventory_Save_Get_Ids() {
        Assert.True(await NpcInventoryService.SaveNpcInventoryAsync(new NpcInventory { TemplateID = 42, Inventory = [100, 200] }));
        Assert.True(await NpcInventoryService.SaveNpcInventoryAsync(new NpcInventory { TemplateID = 465488, Inventory = [9] }));

        Assert.Equal([100UL, 200UL], (await NpcInventoryService.GetNpcInventoryAsync(42))?.Inventory);
        Assert.True(await NpcInventoryService.HasNpcInventoryAsync(465488));
        Assert.Equal(new HashSet<ulong> { 42, 465488 }, await NpcInventoryService.GetNpcTemplateIdsWithInventoriesAsync());
        Assert.Equal(2, (await NpcInventoryService.GetAllNpcInventoriesAsync()).Length);
        var fresh = await LoadFreshStoreAsync();
        Assert.Equal([100UL, 200UL], (await fresh.GetAsync<NpcInventory>("42"))?.Inventory);
        Assert.Equal([9UL], (await fresh.GetAsync<NpcInventory>("465488"))?.Inventory);
        Assert.Equal(2, FilesIn("NpcInventory").Length);
    }

    [Fact]
    public async Task NpcSpellInventory_Save_Delete() {
        var inventory = new NpcSpellInventory(77);
        inventory.Spells.Add(new SpellInventoryItem(500, 0, 10));
        Assert.True(await NpcSpellInventoryService.SaveNpcSpellInventoryAsync(inventory));

        Assert.Equal(500UL, (await NpcSpellInventoryService.GetNpcSpellInventoryAsync(77))?.Spells.Single().TemplateID);
        Assert.Equal(new HashSet<ulong> { 77, 1206770 }, await NpcSpellInventoryService.GetNpcTemplateIdsWithSpellInventoriesAsync());
        var fresh = await LoadFreshStoreAsync();
        Assert.Equal(10, (await fresh.GetAsync<NpcSpellInventory>("77"))?.Spells.Single().Level);

        Assert.True(await NpcSpellInventoryService.DeleteNpcSpellInventoryAsync(77));
        Assert.False(await NpcSpellInventoryService.DeleteNpcSpellInventoryAsync(77));
        Assert.False(await NpcSpellInventoryService.HasNpcSpellInventoryAsync(77));
        Assert.Equal(["NPCSpellInventories_449-A.json"], FilesIn("NpcSpellInventory"));
        Assert.Single(await NpcSpellInventoryService.GetAllNpcSpellInventoriesAsync());
    }

    private async Task<SpiralDbFileStore> LoadFreshStoreAsync() {
        var store = new SpiralDbFileStore(DataRoot);
        var report = await store.LoadAsync();
        Assert.Empty(report.FailedFiles);
        return store;
    }

    private string[] FilesIn(string directory)
        => Directory.GetFiles(Path.Combine(DataRoot, directory)).Select(Path.GetFileName).ToArray()!;
}
