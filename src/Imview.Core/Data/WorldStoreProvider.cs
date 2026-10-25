using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Imview.Core.Common;

namespace Imview.Core.Data;

/// <summary>
/// The one <see cref="IWorldStore"/> the app works on: a <see cref="SpiralDbFileStore"/> over the
/// folder in the <c>DataPack.Path</c> setting, loaded on first use. <see cref="Current"/> throws
/// <see cref="InvalidOperationException"/> when the setting is empty; <see cref="Reload"/> re-reads
/// the setting and the folder on the next access.
/// </summary>
public static class WorldStoreProvider {

    public const string DataPackPathSetting = "DataPack.Path";

    private static readonly object s_lock = new();
    private static IWorldStore? s_current;

    public static IWorldStore Current {
        get {
            lock (s_lock) {
                return s_current ??= Open();
            }
        }
    }

    public static void Reload() {
        lock (s_lock) {
            s_current = null;
        }
    }

    private static IWorldStore Open() {
        var path = ConfigurationManager.GetSetting(DataPackPathSetting);
        if (string.IsNullOrWhiteSpace(path)) {
            throw new InvalidOperationException($"No data pack folder is set: set {DataPackPathSetting} in config.ini.");
        }
        return new LoadedOnceStore(new SpiralDbFileStore(path));
    }

    private sealed class LoadedOnceStore(IWorldStore inner) : IWorldStore {

        private readonly Lazy<Task<WorldStoreLoadReport>> _load = new(inner.LoadAsync);

        public Task<WorldStoreLoadReport> LoadAsync() => _load.Value;

        public async Task<IReadOnlyList<T>> GetAllAsync<T>() where T : class {
            await _load.Value;
            return await inner.GetAllAsync<T>();
        }

        public async Task<T?> GetAsync<T>(string key) where T : class {
            await _load.Value;
            return await inner.GetAsync<T>(key);
        }

        public async Task SaveAsync<T>(T record) where T : class {
            await _load.Value;
            await inner.SaveAsync(record);
        }

        public async Task DeleteAsync<T>(string key) where T : class {
            await _load.Value;
            await inner.DeleteAsync<T>(key);
        }
    }
}
