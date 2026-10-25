using Imcodec.Wad;
using Imview.Core.Services;
using Xunit;

namespace Imview.Tests;

public sealed class KIWadFileServiceTests : IDisposable {

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "imview-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose() {
        if (Directory.Exists(_tempRoot)) {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Unpack_EntryEscapesBase_Skipped() {
        var outputPath = Path.Combine(_tempRoot, "out");
        Directory.CreateDirectory(outputPath);
        var files = new Dictionary<FileEntry, byte[]?> {
            [new FileEntry { FileName = "../escape.txt" }] = "outside"u8.ToArray(),
            [new FileEntry { FileName = "sub/../../escape-nested.txt" }] = "outside"u8.ToArray(),
            [new FileEntry { FileName = "ok/inside.txt" }] = "inside"u8.ToArray(),
        };

        KIWadFileService.WriteArchiveFilesToDisk(files, outputPath, attemptDeserialization: false);

        Assert.False(File.Exists(Path.Combine(_tempRoot, "escape.txt")));
        Assert.False(File.Exists(Path.Combine(_tempRoot, "escape-nested.txt")));
        Assert.Equal("inside", File.ReadAllText(Path.Combine(outputPath, "ok", "inside.txt")));
    }

}
