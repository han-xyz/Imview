using System.Threading.Tasks;
using Imview.Core.Common;

namespace Imview.Core.Data;

public static class DataPack {

    /// <summary>
    /// Whether a data pack folder is set; when it is, waits for <see cref="WorldStoreProvider.Current"/> to finish loading it.
    /// </summary>
    public static async Task<bool> IsLoadedAsync() {
        if (string.IsNullOrWhiteSpace(ConfigurationManager.GetSetting(WorldStoreProvider.DataPackPathSetting))) {
            return false;
        }

        await WorldStoreProvider.Current.LoadAsync();
        return true;
    }
}
