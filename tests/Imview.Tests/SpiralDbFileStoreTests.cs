using System.Text.Json;
using System.Text.Json.Nodes;
using Imcodec.IO;
using Imcodec.ObjectProperty.Bit;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imview.Core.Data;
using Imview.Core.Database.Models;
using Imview.Core.Models;
using Xunit;

namespace Imview.Tests;

public sealed class SpiralDbFileStoreTests : IDisposable {

    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "spiraldb");

    private static readonly string? CloneRoot = Environment.GetEnvironmentVariable("SPIRALDB_CLONE");

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "imview-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose() {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    public static TheoryData<string> FixtureFiles() {
        var files = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(FixtureRoot, "*.json", SearchOption.AllDirectories).Order()) {
            files.Add(Path.GetRelativePath(FixtureRoot, file));
        }
        return files;
    }

    [Theory]
    [MemberData(nameof(FixtureFiles))]
    public async Task SaveUnchanged_RewritesSourceWithoutLoss(string fixture) {
        var directory = Path.GetDirectoryName(fixture)!;
        var source = Path.Combine(FixtureRoot, fixture);
        var copy = Path.Combine(_tempRoot, fixture);
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(source, copy);
        var store = new SpiralDbFileStore(_tempRoot);
        await store.LoadAsync();
        var asLoaded = await SerializeOnlyRecordAsync(store, directory);

        await SaveOnlyRecordAsync(store, directory);

        Assert.Equal([copy], Directory.GetFiles(Path.GetDirectoryName(copy)!));
        var loss = JsonTree.FirstLoss(JsonTree.ParseFile(source), JsonTree.ParseFile(copy));
        Assert.True(loss is null, loss);
        var reloaded = new SpiralDbFileStore(_tempRoot);
        await reloaded.LoadAsync();
        Assert.Equal(asLoaded, await SerializeOnlyRecordAsync(reloaded, directory));
    }

    [CloneFact]
    public async Task Clone_QuestTemplatesLoadAndSaveWithoutLoss() {
        var report = await new SpiralDbFileStore(CloneRoot!).LoadAsync();
        Assert.DoesNotContain(report.FailedFiles, file => file.Contains($"{Path.DirectorySeparatorChar}QuestTemplates{Path.DirectorySeparatorChar}"));

        var questDir = Path.Combine(_tempRoot, "QuestTemplates");
        Directory.CreateDirectory(questDir);
        var sample = Directory.GetFiles(Path.Combine(CloneRoot!, "QuestTemplates"), "*.json");
        foreach (var file in sample) {
            File.Copy(file, Path.Combine(questDir, Path.GetFileName(file)));
        }
        var store = new SpiralDbFileStore(_tempRoot);
        await store.LoadAsync();

        foreach (var quest in await store.GetAllAsync<QuestTemplate>()) {
            await store.SaveAsync(quest);
        }

        foreach (var file in sample) {
            var loss = JsonTree.FirstLoss(
                JsonTree.ParseFile(file),
                JsonTree.ParseFile(Path.Combine(questDir, Path.GetFileName(file))));
            Assert.True(loss is null, $"{Path.GetFileName(file)}: {loss}");
        }
    }

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
    public async Task Save_RenamedRecord_RewritesSourceFile() {
        CopyFixturesTo(_tempRoot);
        var store = new SpiralDbFileStore(_tempRoot);
        await store.LoadAsync();

        var dropTable = (await store.GetAsync<DropTable>("test"))!;
        dropTable.Name = "Renamed";
        await store.SaveAsync(dropTable);

        Assert.Equal(["droptables_test.json"], Directory.GetFiles(Path.Combine(_tempRoot, "DropTables")).Select(Path.GetFileName));
        Assert.Null(await store.GetAsync<DropTable>("test"));
        Assert.Same(dropTable, await store.GetAsync<DropTable>("renamed"));
        var reloaded = new SpiralDbFileStore(_tempRoot);
        await reloaded.LoadAsync();
        Assert.NotNull(await reloaded.GetAsync<DropTable>("Renamed"));
    }

    [SymlinkFact]
    public async Task Save_SymlinkedTempFile_Refused() {
        var pack = Path.Combine(_tempRoot, "pack");
        CopyFixturesTo(pack);
        var victim = Path.Combine(_tempRoot, "outside", "victim.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(victim)!);
        await File.WriteAllTextAsync(victim, "original");
        var source = Path.Combine(pack, "DropTables", "droptables_test.json");
        File.CreateSymbolicLink(source + ".tmp", victim);
        var store = new SpiralDbFileStore(pack);
        await store.LoadAsync();
        var table = (await store.GetAsync<DropTable>("TEST"))!;

        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(table));

        Assert.Equal("original", await File.ReadAllTextAsync(victim));
        Assert.Null(new FileInfo(source).LinkTarget);
    }

    [SymlinkFact]
    public async Task Save_SymlinkedCategoryDir_Refused() {
        var pack = Path.Combine(_tempRoot, "pack");
        var outside = Path.Combine(_tempRoot, "outside");
        CopyFixturesTo(outside);
        Directory.CreateDirectory(pack);
        Directory.CreateSymbolicLink(Path.Combine(pack, "DropTables"), Path.Combine(outside, "DropTables"));
        var outsideFile = Path.Combine(outside, "DropTables", "droptables_test.json");
        var before = await File.ReadAllTextAsync(outsideFile);
        var store = new SpiralDbFileStore(pack);
        await store.LoadAsync();
        var table = (await store.GetAsync<DropTable>("TEST"))!;
        table.Description = "written outside";

        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(table));
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(new DropTable { Name = "NEW" }));

        Assert.Equal(before, await File.ReadAllTextAsync(outsideFile));
        Assert.Equal([outsideFile], Directory.GetFiles(Path.Combine(outside, "DropTables")));
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
        var brokenFile = Path.Combine(_tempRoot, "DropTables", "droptables_broken.json");
        await File.WriteAllTextAsync(brokenFile, "{ \"Name\": ");
        var keylessFile = Path.Combine(_tempRoot, "CreatureSpellbook", "keyless.json");
        await File.WriteAllTextAsync(keylessFile, "{}");
        var store = new SpiralDbFileStore(_tempRoot);

        var report = await store.LoadAsync();

        Assert.Equal([keylessFile, brokenFile], report.FailedFiles.Order(StringComparer.Ordinal));
        Assert.Equal(1, report.CountsByDirectory["DropTables"]);
        Assert.Equal(1, report.CountsByDirectory["CreatureSpellbook"]);
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

    // Expected text is what Imlight's Imcodec converters write: GID `WriteNumberValue(value.Full)`,
    // ByteString and WideByteString `WriteStringValue(value.ToString())`, Bui2/4/5/7, U24, S24 `WriteNumberValue(value.Value)`.
    [Fact]
    public void Converters_MatchImlightWireFormat() {
        var options = SpiralJsonOptions.WriteOptions;
        Assert.Equal("12345678901234", JsonSerializer.Serialize(new GID(12345678901234), options));
        Assert.Equal("\"GH-HERO\"", JsonSerializer.Serialize(new ByteString("GH-HERO"), options));
        Assert.Equal("\"Mooshu's\"", JsonSerializer.Serialize(new WideByteString("Mooshu's"), options));
        Assert.Equal("3", JsonSerializer.Serialize(new Bui2(3), options));
        Assert.Equal("15", JsonSerializer.Serialize(new Bui4(15), options));
        Assert.Equal("31", JsonSerializer.Serialize(new Bui5(31), options));
        Assert.Equal("127", JsonSerializer.Serialize(new Bui7(127), options));
        Assert.Equal("16777215", JsonSerializer.Serialize(new U24(16777215), options));
        Assert.Equal("-8388608", JsonSerializer.Serialize(new S24(-8388608), options));
        Assert.Equal(42UL, JsonSerializer.Deserialize<GID>("\"42\"", SpiralJsonOptions.Options).Full);
        Assert.Equal("GH-HERO", JsonSerializer.Deserialize<ByteString>("\"GH-HERO\"", SpiralJsonOptions.Options).ToString());
    }

    [Fact]
    public async Task QuestTemplate_PolymorphicGoalsSurvive() {
        var sourceFile = Path.Combine(FixtureRoot, "QuestTemplates", "questtemplates_GH-HERO-C03-005.json");
        var source = new SpiralDbFileStore(FixtureRoot);
        await source.LoadAsync();
        var quest = (await source.GetAsync<QuestTemplate>("GH-HERO-C03-005"))!;

        var target = new SpiralDbFileStore(_tempRoot);
        await target.SaveAsync(quest);

        var savedFile = Directory.GetFiles(Path.Combine(_tempRoot, "QuestTemplates")).Single();
        var expected = GoalTypeDiscriminators(sourceFile);
        Assert.NotEmpty(expected);
        Assert.Equal(expected, GoalTypeDiscriminators(savedFile));
    }

    [Fact]
    public async Task QuestTemplate_FractionalWaitSurvives() {
        var source = new SpiralDbFileStore(FixtureRoot);
        await source.LoadAsync();
        var quest = (await source.GetAsync<QuestTemplate>("WC-CYCLOPS-SIDE-005"))!;

        var target = new SpiralDbFileStore(_tempRoot);
        await target.SaveAsync(quest);

        var saved = await File.ReadAllTextAsync(Directory.GetFiles(Path.Combine(_tempRoot, "QuestTemplates")).Single());
        Assert.Contains("\"m_secondsToWait\": 1.5", saved);
    }

    private static async Task SaveOnlyRecordAsync(SpiralDbFileStore store, string directory) {
        switch (directory) {
            case "QuestTemplates": await store.SaveAsync(Assert.Single(await store.GetAllAsync<QuestTemplate>())); break;
            case "QuestMetadatas": await store.SaveAsync(Assert.Single(await store.GetAllAsync<QuestMetadata>())); break;
            case "DropTables": await store.SaveAsync(Assert.Single(await store.GetAllAsync<DropTable>())); break;
            case "NpcInventory": await store.SaveAsync(Assert.Single(await store.GetAllAsync<NpcInventory>())); break;
            case "NpcSpellInventory": await store.SaveAsync(Assert.Single(await store.GetAllAsync<NpcSpellInventory>())); break;
            case "CreatureSpellbook": await store.SaveAsync(Assert.Single(await store.GetAllAsync<CreatureSpellbook>())); break;
            case "ZoneTransfer": await store.SaveAsync(Assert.Single(await store.GetAllAsync<WizardZoneData>())); break;
            default: throw new ArgumentException($"No record type for fixture directory {directory}.");
        }
    }

    private static async Task<string> SerializeOnlyRecordAsync(SpiralDbFileStore store, string directory) => directory switch {
        "QuestTemplates" => Serialize(Assert.Single(await store.GetAllAsync<QuestTemplate>())),
        "QuestMetadatas" => Serialize(Assert.Single(await store.GetAllAsync<QuestMetadata>())),
        "DropTables" => Serialize(Assert.Single(await store.GetAllAsync<DropTable>())),
        "NpcInventory" => Serialize(Assert.Single(await store.GetAllAsync<NpcInventory>())),
        "NpcSpellInventory" => Serialize(Assert.Single(await store.GetAllAsync<NpcSpellInventory>())),
        "CreatureSpellbook" => Serialize(Assert.Single(await store.GetAllAsync<CreatureSpellbook>())),
        "ZoneTransfer" => Serialize(Assert.Single(await store.GetAllAsync<WizardZoneData>())),
        _ => throw new ArgumentException($"No record type for fixture directory {directory}.")
    };

    private static string Serialize<T>(T record) => JsonSerializer.Serialize(record, SpiralJsonOptions.WriteOptions);

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

    private sealed class SymlinkFactAttribute : FactAttribute {
        public SymlinkFactAttribute() {
            var probe = Path.Combine(Path.GetTempPath(), "imview-link-probe-" + Guid.NewGuid().ToString("N"));
            try {
                File.CreateSymbolicLink(probe, Path.GetTempPath());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException) {
                Skip = $"symbolic links cannot be created here: {ex.Message}";
            }
            finally {
                File.Delete(probe);
            }
        }
    }

    private sealed class CloneFactAttribute : FactAttribute {
        public CloneFactAttribute() {
            if (CloneRoot is null || !Directory.Exists(CloneRoot)) {
                Skip = "set SPIRALDB_CLONE to a spiraldb clone to run this test";
            }
        }
    }

}
