// Copyright (c) 2025 Jay Kulsh
// Licensed under the BSD 3-Clause License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imview.Core.Data;
using Imview.Core.Database.Models;

namespace Imview.Core.Database.Services {
    /// <summary>
    /// NPC spell inventories in the data pack's NpcSpellInventory folder (<see cref="WorldStoreProvider.Current"/>)
    /// </summary>
    public static class NpcSpellInventoryService {
        public static async Task<NpcSpellInventory?> GetNpcSpellInventoryAsync(ulong templateId) {
            return await WorldStoreProvider.Current.GetAsync<NpcSpellInventory>(templateId.ToString());
        }

        /// <summary>
        /// Saves a spell inventory, replacing the spells of the inventory with the same template ID
        /// </summary>
        public static async Task<bool> SaveNpcSpellInventoryAsync(NpcSpellInventory spellInventory) {
            var store = WorldStoreProvider.Current;
            var existing = await GetNpcSpellInventoryAsync(spellInventory.TemplateID);
            if (existing == null) {
                existing = spellInventory;
            } else if (existing != spellInventory) {
                existing.Spells.Clear();
                existing.Spells.AddRange(spellInventory.Spells);
            }

            await store.SaveAsync(existing);
            return true;
        }

        /// <returns>False if the NPC has no spell inventory</returns>
        public static async Task<bool> DeleteNpcSpellInventoryAsync(ulong templateId) {
            if (!await HasNpcSpellInventoryAsync(templateId)) {
                return false;
            }

            await WorldStoreProvider.Current.DeleteAsync<NpcSpellInventory>(templateId.ToString());
            return true;
        }

        public static async Task<HashSet<ulong>> GetNpcTemplateIdsWithSpellInventoriesAsync() {
            var inventories = await WorldStoreProvider.Current.GetAllAsync<NpcSpellInventory>();
            return inventories.Select(si => si.TemplateID).ToHashSet();
        }

        public static async Task<bool> HasNpcSpellInventoryAsync(ulong templateId) {
            return await GetNpcSpellInventoryAsync(templateId) != null;
        }

        public static async Task<List<NpcSpellInventory>> GetAllNpcSpellInventoriesAsync() {
            var inventories = await WorldStoreProvider.Current.GetAllAsync<NpcSpellInventory>();
            return inventories.ToList();
        }
    }
}
