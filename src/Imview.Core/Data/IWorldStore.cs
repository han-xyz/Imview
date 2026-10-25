using System.Collections.Generic;
using System.Threading.Tasks;

namespace Imview.Core.Data;

/// <summary>
/// World data by category. <typeparamref name="T"/> is one of the registered record types
/// (QuestTemplate, QuestMetadata, DropTable, NpcInventory, NpcSpellInventory, CreatureSpellbook,
/// WizardZoneData); keys are the record's own key (quest name, table name, template ID, deck name,
/// zone name, quest template ID), compared case-insensitively. Get methods return the store's own
/// instances: <see cref="SaveAsync{T}"/> writes an instance back to the file it was loaded from, even
/// after its key was edited.
/// </summary>
public interface IWorldStore {
    Task<WorldStoreLoadReport> LoadAsync();
    Task<IReadOnlyList<T>> GetAllAsync<T>() where T : class;
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SaveAsync<T>(T record) where T : class;
    Task DeleteAsync<T>(string key) where T : class;
}

public sealed record WorldStoreLoadReport(
    IReadOnlyDictionary<string, int> CountsByDirectory,
    IReadOnlyList<string> FailedFiles,
    IReadOnlyList<string> DuplicateFiles);
