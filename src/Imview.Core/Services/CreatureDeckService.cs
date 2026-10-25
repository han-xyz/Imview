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
using Imview.Core.Database.Models;

namespace Imview.Core.Services;

/// <summary>
/// Creature spell decks in the data pack's CreatureSpellbook folder (<see cref="WorldStoreProvider.Current"/>)
/// </summary>
public static class CreatureDeckService {
    public static async Task<CreatureDeck?> GetCreatureDeckAsync(string deckName) {
        if (string.IsNullOrWhiteSpace(deckName)) {
            return null;
        }

        var doc = await WorldStoreProvider.Current.GetAsync<CreatureSpellbook>(deckName);
        if (doc == null) {
            return null;
        }

        return new CreatureDeck {
            DeckName = doc.DeckName,
            SpellTemplateIds = doc.SpellTemplateIds?.Select(id => (uint) id).ToList() ?? []
        };
    }

    /// <summary>
    /// Saves a creature deck, replacing the spells of the deck with the same name
    /// </summary>
    /// <returns>False if the deck has no name</returns>
    public static async Task<bool> SaveCreatureDeckAsync(CreatureDeck deck) {
        if (deck == null || string.IsNullOrWhiteSpace(deck.DeckName)) {
            return false;
        }

        var store = WorldStoreProvider.Current;
        var stored = await store.GetAsync<CreatureSpellbook>(deck.DeckName);
        var doc = stored == null ? new CreatureSpellbook { DeckName = deck.DeckName } : WorldRecordCopy.Detached(stored);
        doc.SpellTemplateIds = deck.SpellTemplateIds.Select(id => (long) id).ToList();
        await store.SaveAsync(doc);
        return true;
    }

    /// <returns>False if no deck has that name</returns>
    public static async Task<bool> DeleteCreatureDeckAsync(string deckName) {
        if (!await HasCreatureDeckAsync(deckName)) {
            return false;
        }

        await WorldStoreProvider.Current.DeleteAsync<CreatureSpellbook>(deckName);
        return true;
    }

    public static async Task<bool> HasCreatureDeckAsync(string deckName) {
        if (string.IsNullOrWhiteSpace(deckName)) {
            return false;
        }

        return await WorldStoreProvider.Current.GetAsync<CreatureSpellbook>(deckName) != null;
    }

    public static async Task<List<string>> GetAllDeckNamesAsync() {
        var decks = await WorldStoreProvider.Current.GetAllAsync<CreatureSpellbook>();
        return decks.Select(d => d.DeckName).ToList();
    }
}
