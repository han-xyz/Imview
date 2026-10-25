using Xunit;

namespace Imview.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorldStoreProviderCollection {
    public const string Name = "WorldStoreProvider";
}
