using System.Text.Json;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Common;
using Imview.Core.Data;
using Imview.Core.Database.Collections;
using Imview.Core.Database.Models;
using Xunit;

namespace Imview.Tests;

[Collection(WorldStoreProviderCollection.Name)]
public sealed class QuestStoreTests : IDisposable {

    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "spiraldb");

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "imview-tests-" + Guid.NewGuid().ToString("N"));

    private string DataRoot => Path.Combine(_tempRoot, "spiraldb");

    public QuestStoreTests() {
        foreach (var directory in new[] { "QuestTemplates", "QuestMetadatas" }) {
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
    public async Task Upsert_WritesTemplateAndMetadataFiles() {
        var templatesBefore = FilesIn("QuestTemplates");
        var metadataBefore = FilesIn("QuestMetadatas");

        var id = await QuestCollection.UpsertQuestAsync(new QuestTemplate { m_questName = "TEST-NEW-001" }, "Test quest", "From a test");

        Assert.Equal("questtemplates/TEST-NEW-001", id);
        Assert.Single(FilesIn("QuestTemplates").Except(templatesBefore));
        Assert.Single(FilesIn("QuestMetadatas").Except(metadataBefore));
        var fresh = new SpiralDbFileStore(DataRoot);
        var report = await fresh.LoadAsync();
        Assert.Empty(report.FailedFiles);
        Assert.NotNull(await fresh.GetAsync<QuestTemplate>("TEST-NEW-001"));
        var metadata = await fresh.GetAsync<QuestMetadata>("questtemplates/TEST-NEW-001");
        Assert.Equal("Test quest", metadata?.Name);
        Assert.Equal("From a test", metadata?.Description);
    }

    [Fact]
    public async Task Update_RewritesSameFile() {
        var templatesBefore = FilesIn("QuestTemplates");
        var quest = await QuestCollection.GetQuestAsync("GH-HERO-C03-005");
        Assert.NotNull(quest);
        quest.Template.m_questLevel = 42;

        var updated = await QuestCollection.UpdateQuestAsync("questtemplates/GH-HERO-C03-005", quest.Template, "Hero quest", "Edited");

        Assert.True(updated);
        Assert.Equal(templatesBefore, FilesIn("QuestTemplates"));
        var fresh = new SpiralDbFileStore(DataRoot);
        await fresh.LoadAsync();
        Assert.Equal(42, (await fresh.GetAsync<QuestTemplate>("GH-HERO-C03-005"))?.m_questLevel);
        Assert.Equal("Hero quest", (await fresh.GetAsync<QuestMetadata>("questtemplates/GH-HERO-C03-005"))?.Name);
    }

    [Fact]
    public async Task Update_KeepsBehaviors() {
        var incoming = ReadFixtureQuest("GH-HERO-C03-005");
        incoming.m_behaviors = [new BehaviorTemplate { m_behaviorName = "TestBehavior" }];

        Assert.True(await QuestCollection.UpdateQuestAsync("GH-HERO-C03-005", incoming, "Hero quest", ""));

        Assert.Equal(["TestBehavior"], await StoredBehaviorNamesAsync("GH-HERO-C03-005"));
    }

    [Fact]
    public async Task Upsert_KeepsBehaviors() {
        var incoming = ReadFixtureQuest("GH-HERO-C03-005");
        incoming.m_behaviors = [new BehaviorTemplate { m_behaviorName = "TestBehavior" }];

        await QuestCollection.UpsertQuestAsync(incoming, "Hero quest", "");

        Assert.Equal(["TestBehavior"], await StoredBehaviorNamesAsync("GH-HERO-C03-005"));
    }

    [Fact]
    public async Task Update_RenamedQuest_RewritesSameFile() {
        var templatesBefore = FilesIn("QuestTemplates");
        var quest = await QuestCollection.GetQuestAsync("GH-HERO-C03-005");
        quest!.Template.m_questName = "GH-HERO-RENAMED";

        Assert.True(await QuestCollection.UpdateQuestAsync(quest.Id, quest.Template, "Renamed", ""));

        Assert.Equal(templatesBefore, FilesIn("QuestTemplates"));
        Assert.Null(await QuestCollection.GetQuestAsync("GH-HERO-C03-005"));
        Assert.Equal("Renamed", (await QuestCollection.GetQuestAsync("GH-HERO-RENAMED"))?.Name);
    }

    [Fact]
    public async Task QuestMetadata_NoIdOnSave() {
        var metadataFile = Path.Combine(DataRoot, "QuestMetadatas", "questmetadata_GH-HERO-C03-005.json");
        File.WriteAllText(metadataFile, """
            { "Id": "questmetadatas/1-A", "QuestTemplateId": "questtemplates/GH-HERO-C03-005", "Name": "Hero quest" }
            """);
        WorldStoreProvider.Reload();
        var quest = await QuestCollection.GetQuestAsync("GH-HERO-C03-005");

        Assert.True(await QuestCollection.UpdateQuestAsync(quest!.Id, quest.Template, "Hero quest", "Saved"));

        using var saved = JsonDocument.Parse(File.ReadAllText(metadataFile));
        Assert.False(saved.RootElement.TryGetProperty("Id", out _));
        Assert.Equal("Saved", saved.RootElement.GetProperty("Description").GetString());
    }

    [Fact]
    public async Task Update_EmptyQuestName_Rejected() {
        var templatesBefore = FilesIn("QuestTemplates");
        var quest = await QuestCollection.GetQuestAsync("GH-HERO-C03-005");
        quest!.Template.m_questName = "";

        await Assert.ThrowsAsync<ArgumentException>(() => QuestCollection.UpdateQuestAsync(quest.Id, quest.Template, "Nameless", ""));

        Assert.Equal(templatesBefore, FilesIn("QuestTemplates"));
        Assert.NotNull(await QuestCollection.GetQuestAsync("GH-HERO-C03-005"));
    }

    [Fact]
    public void DataPackPath_Empty_ProviderThrowsNamedError() {
        ConfigurationManager.SetSetting(WorldStoreProvider.DataPackPathSetting, "");
        WorldStoreProvider.Reload();

        var error = Assert.Throws<InvalidOperationException>(() => WorldStoreProvider.Current);
        Assert.Contains(WorldStoreProvider.DataPackPathSetting, error.Message);
    }

    [Fact]
    public async Task GetQuest_EditsDoNotReachStoreUntilSaved() {
        var quest = await QuestCollection.GetQuestAsync("GH-HERO-C03-005");
        quest!.Template.m_questLevel = 42;

        Assert.Equal(1, (await QuestCollection.GetQuestAsync("GH-HERO-C03-005"))?.Template.m_questLevel);
        Assert.Equal(1, (await QuestCollection.GetAllQuestsAsync()).Single(q => q.Id == quest.Id).Template.m_questLevel);
    }

    [Fact]
    public void Current_MissingFolder_Throws() {
        ConfigurationManager.SetSetting(WorldStoreProvider.DataPackPathSetting, Path.Combine(_tempRoot, "missing"));
        WorldStoreProvider.Reload();

        Assert.Throws<InvalidOperationException>(() => WorldStoreProvider.Current);
    }

    [Fact]
    public async Task Delete_RemovesBoth() {
        var templatesBefore = FilesIn("QuestTemplates");
        var metadataBefore = FilesIn("QuestMetadatas");
        var id = await QuestCollection.UpsertQuestAsync(new QuestTemplate { m_questName = "TEST-DEL-001" }, "Doomed", "");

        var deleted = await QuestCollection.DeleteQuestAsync(id!);

        Assert.True(deleted);
        Assert.Equal(templatesBefore, FilesIn("QuestTemplates"));
        Assert.Equal(metadataBefore, FilesIn("QuestMetadatas"));
        Assert.Null(await QuestCollection.GetQuestAsync(id!));
    }

    [Fact]
    public async Task FindByName_CaseInsensitive() {
        await QuestCollection.UpsertQuestAsync(new QuestTemplate { m_questName = "TEST-FIND-001" }, "Lost Lamp", "");

        var byMetadataName = await QuestCollection.FindQuestByNameAsync("lost lamp");
        var byQuestName = await QuestCollection.FindQuestByNameAsync("gh-hero-c03-005");

        Assert.Equal("questtemplates/TEST-FIND-001", byMetadataName?.Id);
        Assert.Equal("questtemplates/GH-HERO-C03-005", byQuestName?.Id);
    }

    [Fact]
    public async Task GetAll_Counts() {
        var templateFiles = FilesIn("QuestTemplates").Count;

        Assert.Equal(templateFiles, (await QuestCollection.GetAllQuestsAsync()).Count);
        await QuestCollection.UpsertQuestAsync(new QuestTemplate { m_questName = "TEST-COUNT-001" }, "Counted", "");
        Assert.Equal(templateFiles + 1, (await QuestCollection.GetAllQuestsAsync()).Count);
    }

    private static QuestTemplate ReadFixtureQuest(string questName)
        => JsonSerializer.Deserialize<QuestTemplate>(
            File.ReadAllText(Path.Combine(FixtureRoot, "QuestTemplates", $"questtemplates_{questName}.json")),
            SpiralJsonOptions.Options)!;

    private async Task<List<string>> StoredBehaviorNamesAsync(string questName) {
        var fresh = new SpiralDbFileStore(DataRoot);
        await fresh.LoadAsync();
        var stored = await fresh.GetAsync<QuestTemplate>(questName);
        return [.. (stored?.m_behaviors ?? []).Select(b => b.m_behaviorName.ToString())];
    }

    private List<string> FilesIn(string directory)
        => [.. Directory.GetFiles(Path.Combine(DataRoot, directory)).Order(StringComparer.Ordinal)];
}
