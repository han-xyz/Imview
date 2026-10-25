using Imview.Core.Common;
using Imview.Core.Services;
using Xunit;

namespace Imview.Tests;

[Collection(WorldStoreProviderCollection.Name)]
public sealed class ClientFileServiceTests : IDisposable {

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "imview-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string? _configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

    public ClientFileServiceTests() {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _tempRoot);
        ConfigurationManager.Initialize(Path.Combine(_tempRoot, "config.ini"));
    }

    public void Dispose() {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _configHome);
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("V_r806919.Wizard_1_610", true)]
    [InlineData("..", false)]
    [InlineData(".", false)]
    [InlineData("../x", false)]
    [InlineData("/abs", false)]
    [InlineData("a\\b", false)]
    [InlineData("V_r1\nX", false)]
    [InlineData("V_r1\n", false)]
    [InlineData("", false)]
    public void Revision_Allowlist(string revision, bool expected) {
        Assert.Equal(expected, ClientFileService.IsSafeRevision(revision));
    }

    [Fact]
    public void Download_OutsideCache_Refused() {
        var root = Path.Combine(Path.GetTempPath(), "imview-cache");

        Assert.Throws<InvalidOperationException>(() => ClientFileService.ResolveInside(root, ".."));
        Assert.Throws<InvalidOperationException>(() => ClientFileService.ResolveInside(root, Path.Combine("..", "..", "autostart", "imview.desktop")));
        Assert.Throws<InvalidOperationException>(() => ClientFileService.ResolveInside(root, Path.Combine(Path.GetTempPath(), "elsewhere")));
        Assert.Equal(Path.Combine(root, "V_r1.Wizard_1", "Root.wad"), ClientFileService.ResolveInside(root, Path.Combine("V_r1.Wizard_1", "Root.wad")));
    }

    [Theory]
    [InlineData("V_r806919.Wizard_1_610", "V_r806919.Wizard_1_610")]
    [InlineData("..", "")]
    [InlineData("../../autostart", "")]
    public void SavedRevision_Validated(string saved, string expected) {
        ConfigurationManager.SetSetting("ClientFiles.SelectedRevision", saved);

        Assert.Equal(expected, new ClientFileService().GetSelectedRevision());
    }

}
