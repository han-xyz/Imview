using System;
using System.Threading.Tasks;

namespace Imview.Core.Data;

public static class DataPack {

    /// <summary>
    /// Whether the data pack folder is set and exists; when it does, waits for <see cref="WorldStoreProvider.Current"/> to finish loading it.
    /// </summary>
    public static async Task<bool> IsLoadedAsync() {
        IWorldStore store;
        try {
            store = WorldStoreProvider.Current;
        } catch (InvalidOperationException) {
            return false;
        }

        await store.LoadAsync();
        return true;
    }
}
