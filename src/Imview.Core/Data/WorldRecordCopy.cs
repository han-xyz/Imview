using System.Text.Json;

namespace Imview.Core.Data;

public static class WorldRecordCopy {

    /// <summary>
    /// A copy of a world record that shares no objects with it, made by a round trip through the data pack's JSON options.
    /// </summary>
    public static T Detached<T>(T record) where T : class
        => JsonSerializer.Deserialize<T>(
            JsonSerializer.SerializeToUtf8Bytes(record, SpiralJsonOptions.WriteOptions),
            SpiralJsonOptions.Options)!;
}
