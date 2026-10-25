/*
BSD 3-Clause License

Copyright (c) 2024, Jooty

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.
*/

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imview.Core.Data;
using Imview.Core.Models;

namespace Imview.Core.Services;

/// <summary>
/// NPC inventories in the data pack's NpcInventory folder (<see cref="WorldStoreProvider.Current"/>)
/// </summary>
public static class NpcInventoryService {
    public static async Task<NpcInventory?> GetNpcInventoryAsync(ulong templateId) {
        return await WorldStoreProvider.Current.GetAsync<NpcInventory>(templateId.ToString());
    }

    public static async Task<bool> HasNpcInventoryAsync(ulong templateId) {
        return await GetNpcInventoryAsync(templateId) != null;
    }

    /// <summary>
    /// Saves an NPC inventory, replacing the items of the inventory with the same template ID
    /// </summary>
    public static async Task<bool> SaveNpcInventoryAsync(NpcInventory npcInventory) {
        var store = WorldStoreProvider.Current;
        var existing = await GetNpcInventoryAsync(npcInventory.TemplateID);
        if (existing == null) {
            existing = npcInventory;
        } else {
            existing.Inventory = npcInventory.Inventory;
        }

        await store.SaveAsync(existing);
        return true;
    }

    public static async Task<HashSet<ulong>> GetNpcTemplateIdsWithInventoriesAsync() {
        var inventories = await WorldStoreProvider.Current.GetAllAsync<NpcInventory>();
        return inventories.Select(ni => ni.TemplateID).ToHashSet();
    }

    public static async Task<NpcInventory[]> GetAllNpcInventoriesAsync() {
        var inventories = await WorldStoreProvider.Current.GetAllAsync<NpcInventory>();
        return inventories.ToArray();
    }
}
