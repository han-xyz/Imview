using Imview.Core.Services;
using Xunit;

namespace Imview.Tests;

public class ClientFileServiceTests {

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

}
