using System.Globalization;
using Imview.Core.Common;
using Imview.Core.Data;
using Imview.Core.Models;
using Imview.Core.Services;
using Xunit;

namespace Imview.Tests;

[Collection(WorldStoreProviderCollection.Name)]
public sealed class ZoneTransferTests : IDisposable {

    private const string FixtureZone = "MooShu/Interiors/MS_ShopClothing";
    private const string FixtureTrigger = "ToJadePalace";
    private const string FixtureFile = "WizardZoneDatas_2148-A.json";

    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "spiraldb");

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "imview-tests-" + Guid.NewGuid().ToString("N"));

    private string DataRoot => Path.Combine(_tempRoot, "spiraldb");

    private string ZoneTransferDir => Path.Combine(DataRoot, "ZoneTransfer");

    public ZoneTransferTests() {
        Directory.CreateDirectory(ZoneTransferDir);
        File.Copy(Path.Combine(FixtureRoot, "ZoneTransfer", FixtureFile), Path.Combine(ZoneTransferDir, FixtureFile));
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
    public async Task Teleport_Save_RewritesZoneFile() {
        var zone = await ZoneTransferService.GetZoneTransferAsync(FixtureZone);
        Assert.NotNull(zone);
        zone.Teleports.Single(t => t.TriggerName == FixtureTrigger).Teleport.m_destinationZone = "MooShu/MS_Palace";

        await ZoneTransferService.SaveZoneTransferAsync(zone);

        Assert.Equal([FixtureFile], FileNames());
        var fresh = await LoadFreshStoreAsync();
        var saved = await fresh.GetAsync<WizardZoneData>(FixtureZone);
        Assert.Equal("MooShu/MS_Palace", saved?.Teleports.Single().Teleport.m_destinationZone);
        Assert.Equal("MooShu/MS_Palace", (await ZoneTransferService.GetTeleportDataAsync(FixtureZone, FixtureTrigger))?.m_destinationZone);
    }

    [Fact]
    public async Task Save_NewZone_CreatesFile() {
        var zone = new WizardZoneData {
            ZoneName = "Test/NewZone",
            Teleports = [new WizardTeleportData { TriggerName = "ToHub" }],
        };
        zone.Teleports[0].Teleport.m_destinationZone = "MooShu/MS_Hub";

        await ZoneTransferService.SaveZoneTransferAsync(zone);

        Assert.Contains("zonetransfer_Test-NewZone.json", FileNames());
        var fresh = await LoadFreshStoreAsync();
        Assert.Equal("MooShu/MS_Hub", (await fresh.GetAsync<WizardZoneData>("Test/NewZone"))?.Teleports.Single().Teleport.m_destinationZone);
        Assert.True(await ZoneTransferService.HasTeleportDataAsync("Test/NewZone", "ToHub"));
    }

    [Fact]
    public async Task Delete_RemovesFile() {
        await ZoneTransferService.DeleteZoneTransferAsync(FixtureZone);

        Assert.Empty(FileNames());
        Assert.Null(await ZoneTransferService.GetTeleportDataAsync(FixtureZone, FixtureTrigger));
        Assert.Empty(await ZoneTransferService.GetAllZoneTransfersAsync());
    }

    [Fact]
    public async Task DestinationLoc_InvariantCulture() {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try {
            var zone = await ZoneTransferService.GetZoneTransferAsync(FixtureZone);
            Assert.NotNull(zone);
            zone.Teleports.Single().Teleport.m_destinationLoc = ZoneTransferService.FormatDestinationLoc(12.5f, -3.25f, 50f, 1.5f);

            await ZoneTransferService.SaveZoneTransferAsync(zone);
        } finally {
            CultureInfo.CurrentCulture = culture;
        }

        Assert.Contains("\"m_destinationLoc\": \"12.5,-3.25,50,1.5\"", File.ReadAllText(Path.Combine(ZoneTransferDir, FixtureFile)));
    }

    [Fact]
    public async Task Teleport_EditsDoNotReachStoreUntilSaved() {
        var teleport = await ZoneTransferService.GetTeleportDataAsync(FixtureZone, FixtureTrigger);
        Assert.NotNull(teleport);
        teleport.m_destinationZone = "unsaved";
        var zone = (await ZoneTransferService.GetAllZoneTransfersAsync()).Single();
        Assert.Equal("MooShu/MS_Hub", zone.Teleports.Single().Teleport.m_destinationZone);
        zone.Teleports.Single().Teleport.m_destinationZone = "unsaved";

        Assert.Equal("MooShu/MS_Hub", (await ZoneTransferService.GetTeleportDataAsync(FixtureZone, FixtureTrigger))?.m_destinationZone);

        zone.Teleports.Single().Teleport.m_destinationZone = "saved";
        await ZoneTransferService.SaveZoneTransferAsync(zone);
        zone.Teleports.Single().Teleport.m_destinationZone = "after save";

        Assert.Equal("saved", (await ZoneTransferService.GetTeleportDataAsync(FixtureZone, FixtureTrigger))?.m_destinationZone);
    }

    private string[] FileNames()
        => Directory.GetFiles(ZoneTransferDir).Select(Path.GetFileName).Order().ToArray()!;

    private async Task<SpiralDbFileStore> LoadFreshStoreAsync() {
        var store = new SpiralDbFileStore(DataRoot);
        var report = await store.LoadAsync();
        Assert.Empty(report.FailedFiles);
        return store;
    }
}
