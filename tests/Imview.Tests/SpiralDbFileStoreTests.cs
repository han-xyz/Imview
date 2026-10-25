using System.Text.Json;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imview.Core.Data;
using Imview.Core.Database.Models;
using Imview.Core.Models;
using Xunit;

namespace Imview.Tests;

public sealed class SpiralDbFileStoreTests : IDisposable {

    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "spiraldb");

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "imview-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose() {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public Task RoundTrip_QuestTemplate() => AssertRoundTrips<QuestTemplate>();

    [Fact]
    public Task RoundTrip_QuestMetadata() => AssertRoundTrips<QuestMetadata>();

    [Fact]
    public Task RoundTrip_DropTable() => AssertRoundTrips<DropTable>();

    [Fact]
    public Task RoundTrip_NpcInventory() => AssertRoundTrips<NpcInventory>();

    [Fact]
    public Task RoundTrip_NpcSpellInventory() => AssertRoundTrips<NpcSpellInventory>();

    [Fact]
    public Task RoundTrip_CreatureSpellbook() => AssertRoundTrips<CreatureSpellbook>();

    [Fact]
    public Task RoundTrip_ZoneTransfer() => AssertRoundTrips<WizardZoneData>();

    [Fact]
    public async Task Save_Existing_RewritesSourceFile() {
        CopyFixturesTo(_tempRoot);
        var store = new SpiralDbFileStore(_tempRoot);
        await store.LoadAsync();

        var dropTable = (await store.GetAsync<DropTable>("test"))!;
        dropTable.Description = "edited in Imview";
        await store.SaveAsync(dropTable);

        var dir = Path.Combine(_tempRoot, "DropTables");
        Assert.Equal(["droptables_test.json"], Directory.GetFiles(dir).Select(Path.GetFileName));
        var reloaded = new SpiralDbFileStore(_tempRoot);
        await reloaded.LoadAsync();
        Assert.Equal("edited in Imview", (await reloaded.GetAsync<DropTable>("TEST"))!.Description);
    }

    [Fact]
    public async Task Save_New_UsesTypeKeyName() {
        var store = new SpiralDbFileStore(_tempRoot);
        await store.LoadAsync();

        await store.SaveAsync(new WizardZoneData { ZoneName = "WizardCity/WC Hub" });

        Assert.True(File.Exists(Path.Combine(_tempRoot, "ZoneTransfer", "zonetransfer_WizardCity-WC-Hub.json")));
        var reloaded = new SpiralDbFileStore(_tempRoot);
        await reloaded.LoadAsync();
        Assert.NotNull(await reloaded.GetAsync<WizardZoneData>("WizardCity/WC Hub"));
    }

    [Fact]
    public async Task Load_SkipsAndReportsBadFile() {
        CopyFixturesTo(_tempRoot);
        var badFile = Path.Combine(_tempRoot, "DropTables", "droptables_broken.json");
        await File.WriteAllTextAsync(badFile, "{ \"Name\": ");
        var store = new SpiralDbFileStore(_tempRoot);

        var report = await store.LoadAsync();

        Assert.Equal([badFile], report.FailedFiles);
        Assert.All(report.CountsByDirectory.Values, count => Assert.Equal(1, count));
        Assert.Equal(7, report.CountsByDirectory.Count);
        Assert.Single(await store.GetAllAsync<DropTable>());
    }

    [Fact]
    public async Task Load_ReportsDuplicateKeys() {
        CopyFixturesTo(_tempRoot);
        var dir = Path.Combine(_tempRoot, "CreatureSpellbook");
        var original = Directory.GetFiles(dir).Single();
        var duplicate = Path.Combine(dir, "zz-duplicate.json");
        await File.WriteAllTextAsync(duplicate, "{ \"DeckName\": \"MDECK-MIX-R1-1\", \"SpellTemplateIds\": [] }");
        var store = new SpiralDbFileStore(_tempRoot);

        var report = await store.LoadAsync();

        Assert.Equal([duplicate], report.DuplicateFiles);
        Assert.Empty(report.FailedFiles);
        Assert.Equal(1, report.CountsByDirectory["CreatureSpellbook"]);
        var spellbook = (await store.GetAsync<CreatureSpellbook>("Mdeck-Mix-R1-1"))!;
        Assert.NotEmpty(spellbook.SpellTemplateIds);
        await store.SaveAsync(spellbook);
        Assert.Equal("{ \"DeckName\": \"MDECK-MIX-R1-1\", \"SpellTemplateIds\": [] }", await File.ReadAllTextAsync(duplicate));
        Assert.Contains("SpellTemplateIds", await File.ReadAllTextAsync(original));
    }

    // Expected text is what Imlight's Imcodec converters write: GIDConverter
    // `writer.WriteNumberValue(value.Full)`, ByteStringJsonConverter `writer.WriteStringValue(value.ToString())`.
    [Fact]
    public void Converters_MatchImlightWireFormat() {
        Assert.Equal("12345678901234", JsonSerializer.Serialize(new GID(12345678901234), SpiralJsonOptions.Options));
        Assert.Equal("\"GH-HERO\"", JsonSerializer.Serialize(new ByteString("GH-HERO"), SpiralJsonOptions.Options));
        Assert.Equal(42UL, JsonSerializer.Deserialize<GID>("\"42\"", SpiralJsonOptions.Options).Full);
        Assert.Equal(0UL, JsonSerializer.Deserialize<GID>("null", SpiralJsonOptions.Options).Full);
        Assert.Equal("GH-HERO", JsonSerializer.Deserialize<ByteString>("\"GH-HERO\"", SpiralJsonOptions.Options).ToString());
    }

    [Fact]
    public async Task QuestTemplate_PolymorphicGoalsSurvive() {
        var source = new SpiralDbFileStore(FixtureRoot);
        await source.LoadAsync();
        var quest = Assert.Single(await source.GetAllAsync<QuestTemplate>());

        var target = new SpiralDbFileStore(_tempRoot);
        await target.SaveAsync(quest);

        var sourceFile = Directory.GetFiles(Path.Combine(FixtureRoot, "QuestTemplates")).Single();
        var savedFile = Directory.GetFiles(Path.Combine(_tempRoot, "QuestTemplates")).Single();
        var expected = GoalTypeDiscriminators(sourceFile);
        Assert.NotEmpty(expected);
        Assert.Equal(expected, GoalTypeDiscriminators(savedFile));
    }

    private async Task AssertRoundTrips<T>() where T : class {
        var source = new SpiralDbFileStore(FixtureRoot);
        await source.LoadAsync();
        var original = Assert.Single(await source.GetAllAsync<T>());

        var target = new SpiralDbFileStore(_tempRoot);
        await target.LoadAsync();
        await target.SaveAsync(original);
        var reloaded = new SpiralDbFileStore(_tempRoot);
        await reloaded.LoadAsync();

        Assert.Equal(
            JsonSerializer.Serialize(original, SpiralJsonOptions.Options),
            JsonSerializer.Serialize(Assert.Single(await reloaded.GetAllAsync<T>()), SpiralJsonOptions.Options));
    }

    private static List<string> GoalTypeDiscriminators(string path) {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("m_goals").EnumerateArray()
            .Select(goal => goal.GetProperty("$type").GetString()!)
            .ToList();
    }

    private static void CopyFixturesTo(string root) {
        foreach (var file in Directory.GetFiles(FixtureRoot, "*.json", SearchOption.AllDirectories)) {
            var destination = Path.Combine(root, Path.GetRelativePath(FixtureRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }

}
