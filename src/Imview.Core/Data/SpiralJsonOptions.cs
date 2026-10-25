using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Imcodec.IO;
using Imcodec.ObjectProperty;
using Imcodec.Types;

namespace Imview.Core.Data;

// Copy of Imlight.CoreLib.WizardData.SpiralJsonOptions: Imlight must read what Imview writes.
// Imview's Imcodec submodule only carries Newtonsoft converters for GID and ByteString, so the
// System.Text.Json converters from Imlight's Imcodec are copied here until the submodule is bumped.
public static class SpiralJsonOptions {

    public static readonly JsonSerializerOptions Options = CreateOptions();

    public static readonly JsonSerializerOptions WriteOptions = new(Options) { WriteIndented = true };

    private static JsonSerializerOptions CreateOptions() {
        var resolver = new DefaultJsonTypeInfoResolver();

        var imcodecAssembly = typeof(PropertyClass).Assembly;
        var assemblyName = imcodecAssembly.GetName().Name;

        var concreteTypes = imcodecAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(PropertyClass).IsAssignableFrom(t))
            .ToList();

        resolver.Modifiers.Add(typeInfo => {
            if (!typeof(PropertyClass).IsAssignableFrom(typeInfo.Type) || typeInfo.Type.IsSealed) {
                return;
            }

            var derivedTypes = concreteTypes
                .Where(t => t != typeInfo.Type && typeInfo.Type.IsAssignableFrom(t))
                .ToList();

            if (derivedTypes.Count == 0) {
                return;
            }

            var poly = new JsonPolymorphismOptions {
                TypeDiscriminatorPropertyName = "$type",
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType
            };

            if (!typeInfo.Type.IsAbstract) {
                poly.DerivedTypes.Add(new JsonDerivedType(typeInfo.Type, $"{typeInfo.Type.FullName}, {assemblyName}"));
            }

            foreach (var derived in derivedTypes) {
                poly.DerivedTypes.Add(new JsonDerivedType(derived, $"{derived.FullName}, {assemblyName}"));
            }

            typeInfo.PolymorphismOptions = poly;
        });

        return new JsonSerializerOptions {
            TypeInfoResolver = resolver,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = {
                new JsonStringEnumConverter(),
                new GIDConverter(),
                new ByteStringJsonConverter(),
            }
        };
    }

    private sealed class GIDConverter : JsonConverter<GID> {
        public override GID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch {
                JsonTokenType.Number when reader.TryGetUInt64(out var val) => new GID(val),
                JsonTokenType.String when ulong.TryParse(reader.GetString(), out var val) => new GID(val),
                JsonTokenType.Null => new GID(0),
                _ => throw new JsonException($"Cannot convert {reader.TokenType} to GID.")
            };

        public override void Write(Utf8JsonWriter writer, GID value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value.Full);
    }

    private sealed class ByteStringJsonConverter : JsonConverter<ByteString> {
        public override ByteString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString() ?? string.Empty);

        public override void Write(Utf8JsonWriter writer, ByteString value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString() ?? string.Empty);
    }

}
