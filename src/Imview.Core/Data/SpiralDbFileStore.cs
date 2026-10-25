using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuestTemplate = Imcodec.ObjectProperty.TypeCache.QuestTemplate;
using Imview.Core.Database.Models;
using Imview.Core.Models;

namespace Imview.Core.Data;

public sealed partial class SpiralDbFileStore : IWorldStore {

    private static readonly IReadOnlyList<WorldCategory> Categories = [
        new(typeof(QuestTemplate), "QuestTemplates", r => ((QuestTemplate) r).m_questName),
        new(typeof(QuestMetadata), "QuestMetadatas", r => ((QuestMetadata) r).QuestTemplateId),
        new(typeof(DropTable), "DropTables", r => ((DropTable) r).Name),
        new(typeof(NpcInventory), "NpcInventory", r => ((NpcInventory) r).TemplateID.ToString()),
        new(typeof(NpcSpellInventory), "NpcSpellInventory", r => ((NpcSpellInventory) r).TemplateID.ToString()),
        new(typeof(CreatureSpellbook), "CreatureSpellbook", r => ((CreatureSpellbook) r).DeckName),
        new(typeof(WizardZoneData), "ZoneTransfer", r => ((WizardZoneData) r).ZoneName),
    ];

    private readonly string _root;
    private readonly Dictionary<Type, CategoryRecords> _records =
        Categories.ToDictionary(c => c.RecordType, c => new CategoryRecords(c));

    public SpiralDbFileStore(string root) {
        _root = root;
    }

    public async Task<WorldStoreLoadReport> LoadAsync() {
        var counts = new Dictionary<string, int>();
        var failedFiles = new List<string>();
        var duplicateFiles = new List<string>();

        foreach (var records in _records.Values) {
            records.Clear();
            var dir = Path.Combine(_root, records.Category.Directory);
            if (Directory.Exists(dir)) {
                foreach (var file in Directory.GetFiles(dir, "*.json").Order(StringComparer.Ordinal)) {
                    var record = await TryReadAsync(file, records.Category.RecordType);
                    if (record is null || string.IsNullOrEmpty(records.Category.KeyOf(record))) {
                        failedFiles.Add(file);
                    } else if (records.Find(records.Category.KeyOf(record)) is not null) {
                        duplicateFiles.Add(file);
                    } else {
                        records.Add(record, file);
                    }
                }
            }
            counts[records.Category.Directory] = records.Count;
        }

        return new WorldStoreLoadReport(counts, failedFiles, duplicateFiles);
    }

    public Task<IReadOnlyList<T>> GetAllAsync<T>() where T : class
        => Task.FromResult<IReadOnlyList<T>>(RecordsOf<T>().All.Cast<T>().ToList());

    public Task<T?> GetAsync<T>(string key) where T : class
        => Task.FromResult(RecordsOf<T>().Find(key) as T);

    public async Task SaveAsync<T>(T record) where T : class {
        var records = RecordsOf<T>();
        var path = records.SourcePathOf(record) ?? NewFilePath(records.Category, records.Category.KeyOf(record));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        EnsureNoLinkInsideRoot(path);
        EnsureNoLinkInsideRoot(tempPath);
        File.Delete(tempPath);
        try {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write)) {
                await JsonSerializer.SerializeAsync(stream, record, typeof(T), SpiralJsonOptions.WriteOptions);
                stream.WriteByte((byte) '\n');
            }
            File.Move(tempPath, path, overwrite: true);
        } catch {
            File.Delete(tempPath);
            throw;
        }

        records.Add(record, path);
    }

    public Task DeleteAsync<T>(string key) where T : class {
        var path = RecordsOf<T>().Remove(key);
        if (path is not null) {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private CategoryRecords RecordsOf<T>()
        => _records.TryGetValue(typeof(T), out var records)
            ? records
            : throw new ArgumentException($"{typeof(T).Name} is not a world store record type.");

    // A data pack comes from other people's commits: a committed symbolic link would make a save
    // write to wherever the link points.
    private void EnsureNoLinkInsideRoot(string path) {
        var root = Path.GetFullPath(_root);
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
            throw new IOException($"Refusing to write {fullPath}: it is outside the data pack folder {root}.");
        }

        for (var current = fullPath; current.Length > root.Length; current = Path.GetDirectoryName(current)!) {
            if (new FileInfo(current).LinkTarget is not null) {
                throw new IOException($"Refusing to write {fullPath}: {current} is a symbolic link.");
            }
        }
    }

    private string NewFilePath(WorldCategory category, string key) {
        var path = Path.Combine(_root, category.Directory,
            $"{category.Directory.ToLowerInvariant()}_{UnsafeFileNameChars().Replace(key, "-")}.json");
        if (File.Exists(path)) {
            throw new IOException($"Cannot save {category.RecordType.Name} '{key}': {path} already holds another record.");
        }
        return path;
    }

    private static async Task<object?> TryReadAsync(string file, Type recordType) {
        try {
            await using var stream = File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync(stream, recordType, SpiralJsonOptions.Options);
        } catch (Exception) {
            return null;
        }
    }

    [GeneratedRegex("[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeFileNameChars();

    private sealed record WorldCategory(Type RecordType, string Directory, Func<object, string> KeyOf);

    private sealed class CategoryRecords(WorldCategory category) {
        private readonly Dictionary<string, object> _byKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<object, Source> _sourceByRecord = new(ReferenceEqualityComparer.Instance);

        public WorldCategory Category { get; } = category;

        public int Count => _byKey.Count;

        public IEnumerable<object> All => _byKey.Values;

        public object? Find(string key) => _byKey.GetValueOrDefault(key);

        public string? SourcePathOf(object record) {
            var key = Category.KeyOf(record);
            var holder = _byKey.GetValueOrDefault(key);
            if (_sourceByRecord.TryGetValue(record, out var source)) {
                if (holder is not null && holder != record) {
                    throw new InvalidOperationException(
                        $"Cannot rename {Category.RecordType.Name} to '{key}': {_sourceByRecord[holder].Path} already holds that key.");
                }
                return source.Path;
            }
            return holder is null ? null : _sourceByRecord[holder].Path;
        }

        public void Add(object record, string path) {
            if (_sourceByRecord.Remove(record, out var previous)) {
                _byKey.Remove(previous.Key);
            }
            var key = Category.KeyOf(record);
            if (_byKey.TryGetValue(key, out var replaced)) {
                _sourceByRecord.Remove(replaced);
            }
            _byKey[key] = record;
            _sourceByRecord[record] = new Source(key, path);
        }

        public string? Remove(string key) {
            if (!_byKey.Remove(key, out var record)) {
                return null;
            }
            _sourceByRecord.Remove(record, out var source);
            return source.Path;
        }

        public void Clear() {
            _byKey.Clear();
            _sourceByRecord.Clear();
        }

        private readonly record struct Source(string Key, string Path);
    }

}
