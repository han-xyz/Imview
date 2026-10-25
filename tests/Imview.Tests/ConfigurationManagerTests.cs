using Imview.Core.Common;
using Xunit;

namespace Imview.Tests;

[Collection(WorldStoreProviderCollection.Name)]
public class ConfigurationManagerTests : IDisposable {

    private readonly string _iniPath = Path.Combine(Path.GetTempPath(), $"imview-{Guid.NewGuid():N}.ini");

    public void Dispose() {
        File.Delete(_iniPath);
    }

    [Fact]
    public void SetSetting_RejectsNewline() {
        File.WriteAllText(_iniPath, "[ClientFiles]\nSelectedRevision = \n");
        ConfigurationManager.Initialize(_iniPath);

        Assert.Throws<ArgumentException>(() => ConfigurationManager.SetSetting("ClientFiles.SelectedRevision", "V_r1\n[Database]\nWorldDatabaseUrl = http://evil"));
        Assert.Throws<ArgumentException>(() => ConfigurationManager.SetSetting("ClientFiles.SelectedRevision", "V_r1\rX"));
        Assert.DoesNotContain("evil", File.ReadAllText(_iniPath));
    }

    [Theory]
    [InlineData("ClientFiles.Sel]ected")]
    [InlineData("[Database")]
    [InlineData("ClientFiles.Key = x")]
    [InlineData("ClientFiles.Key\nX")]
    public void SetSetting_RejectsBracketKey(string key) {
        File.WriteAllText(_iniPath, "[ClientFiles]\nSelectedRevision = \n");
        ConfigurationManager.Initialize(_iniPath);

        Assert.Throws<ArgumentException>(() => ConfigurationManager.SetSetting(key, "v"));
    }

    [Fact]
    public void Load_MergesDuplicateSection() {
        File.WriteAllText(_iniPath, "[A]\nFirst = 1\n[B]\nOther = 2\n[A]\nSecond = 3\n");
        ConfigurationManager.Initialize(_iniPath);

        var section = ConfigurationManager.GetSection("A");

        Assert.Equal("1", section["First"]);
        Assert.Equal("3", section["Second"]);
    }

}
