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
            var inventory = await WorldStoreProvider.Current.GetAsync<NpcSpellInventory>(templateId.ToString());
            return inventory == null ? null : WorldRecordCopy.Detached(inventory);
        }

        /// <summary>
        /// Saves a copy of the spell inventory, replacing the spell inventory with the same template ID
        /// </summary>
        public static async Task<bool> SaveNpcSpellInventoryAsync(NpcSpellInventory spellInventory) {
            await WorldStoreProvider.Current.SaveAsync(WorldRecordCopy.Detached(spellInventory));
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
            return inventories.Select(WorldRecordCopy.Detached).ToList();
        }
    }
}
