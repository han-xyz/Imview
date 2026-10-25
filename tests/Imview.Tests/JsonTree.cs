using System.Text.Json;
using System.Text.Json.Nodes;

namespace Imview.Tests;

internal static class JsonTree {

    private static readonly JsonDocumentOptions SpiralDocumentOptions = new() {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static JsonNode? ParseFile(string path) => JsonNode.Parse(File.ReadAllText(path), documentOptions: SpiralDocumentOptions);

    // Returns the path of the first value in `expected` that `actual` lacks or holds differently, or null.
    // Object property order is ignored; numbers compare by value, so 1.0 equals 1, and a float
    // field's widened text (594.9937133789062) equals its shortest form (594.9937); timestamps compare
    // as instants (.571720Z equals .57172Z). A null may come back
    // as "": Imcodec's ByteString converter (Imlight's too) reads null as an empty string.
    public static string? FirstLoss(JsonNode? expected, JsonNode? actual, string path = "$") {
        switch (expected) {
            case null:
                return actual is null || (actual is JsonValue text && text.GetValueKind() == JsonValueKind.String && text.GetValue<string>() == "")
                    ? null
                    : $"{path} (null vs {actual.ToJsonString()})";
            case JsonObject expectedObject:
                if (actual is not JsonObject actualObject) {
                    return path;
                }
                foreach (var (name, value) in expectedObject) {
                    if (!actualObject.ContainsKey(name)) {
                        return $"{path}.{name} (missing)";
                    }
                    var loss = FirstLoss(value, actualObject[name], $"{path}.{name}");
                    if (loss is not null) {
                        return loss;
                    }
                }
                return null;
            case JsonArray expectedArray:
                if (actual is not JsonArray actualArray || actualArray.Count != expectedArray.Count) {
                    return path;
                }
                for (var i = 0; i < expectedArray.Count; i++) {
                    var loss = FirstLoss(expectedArray[i], actualArray[i], $"{path}[{i}]");
                    if (loss is not null) {
                        return loss;
                    }
                }
                return null;
            default:
                return actual is JsonValue && ValuesEqual(expected.AsValue(), actual.AsValue()) ? null : $"{path} ({expected.ToJsonString()} vs {actual?.ToJsonString()})";
        }
    }

    private static bool ValuesEqual(JsonValue expected, JsonValue actual) {
        if (expected.GetValueKind() == JsonValueKind.Number && actual.GetValueKind() == JsonValueKind.Number) {
            return expected.GetValue<double>() == actual.GetValue<double>()
                || expected.GetValue<float>() == actual.GetValue<float>();
        }
        if (expected.TryGetValue<DateTimeOffset>(out var expectedTime) && actual.TryGetValue<DateTimeOffset>(out var actualTime)) {
            return expectedTime == actualTime;
        }
        return expected.ToJsonString() == actual.ToJsonString();
    }

}
