using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Imcodec.ObjectProperty;

namespace Imview.Core.Data;

// Copy of Imlight.CoreLib.WizardData.SpiralJsonOptions: Imlight must read what Imview writes.
public static class SpiralJsonOptions {

    public static readonly JsonSerializerOptions Options = CreateOptions();

    // Reads keep Imlight's options; writes keep nulls and leave non-ASCII and quotes unescaped so an
    // unedited record rewrites to the same JSON as its source file.
    public static readonly JsonSerializerOptions WriteOptions = new(Options) {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly ConditionalWeakTable<object, Dictionary<string, JsonElement>> UnmodelledProperties = new();

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
        resolver.Modifiers.Add(KeepUnmodelledProperties);

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
            }
        };
    }

    // Real files carry properties neither Imview's nor Imlight's types declare (redundant "$type" tags,
    // fields from other tools); they are kept beside the record and written back so a save loses nothing.
    private static void KeepUnmodelledProperties(JsonTypeInfo typeInfo) {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || typeInfo.Type.IsValueType || typeInfo.Properties.Any(p => p.IsExtensionData)) {
            return;
        }

        var property = typeInfo.CreateJsonPropertyInfo(typeof(Dictionary<string, JsonElement>), "$unmodelled");
        property.IsExtensionData = true;
        property.Get = record => UnmodelledProperties.TryGetValue(record, out var values) ? values : null;
        property.Set = (record, values) => UnmodelledProperties.AddOrUpdate(record, (Dictionary<string, JsonElement>) values!);
        typeInfo.Properties.Add(property);
    }

}
