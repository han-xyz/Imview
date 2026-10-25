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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Data;
using Imview.Core.Database.Models;

namespace Imview.Core.Database.Collections;

/// <summary>
/// A quest template with its editor metadata
/// </summary>
public class QuestDocument {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public QuestTemplate Template { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = Environment.UserName;
    public string ModifiedBy { get; set; } = Environment.UserName;
}

/// <summary>
/// Quest templates and their metadata in the data pack (<see cref="WorldStoreProvider.Current"/>).
/// A quest ID is <c>questtemplates/&lt;m_questName&gt;</c>; methods taking an ID also accept the bare quest name.
/// Templates are handed out and stored as copies: an edit reaches the data pack only through an update or upsert.
/// </summary>
public static class QuestCollection {

    private const string TemplateIdPrefix = "questtemplates/";

    /// <summary>
    /// Saves a quest template and its metadata, replacing the quest with the same m_questName
    /// </summary>
    /// <returns>The ID of the saved quest</returns>
    public static Task<string?> SaveQuestAsync(QuestTemplate questTemplate, string name, string description = "")
        => UpsertQuestAsync(questTemplate, name, description);

    /// <summary>
    /// Updates an existing quest; renaming m_questName moves the quest to the new ID
    /// </summary>
    /// <returns>False if no quest has <paramref name="questId"/></returns>
    public static async Task<bool> UpdateQuestAsync(string questId, QuestTemplate questTemplate, string name, string description = "") {
        if (questTemplate.m_questName.IsEmpty) {
            throw new ArgumentException("Quest name (m_questName) cannot be empty.", nameof(questTemplate));
        }

        var store = WorldStoreProvider.Current;
        var existing = await store.GetAsync<QuestTemplate>(QuestNameOf(questId));
        if (existing == null) {
            return false;
        }

        var metadataId = TemplateIdOf(existing.m_questName);
        if (!string.Equals(existing.m_questName, questTemplate.m_questName, StringComparison.OrdinalIgnoreCase)) {
            if (await store.GetAsync<QuestTemplate>(questTemplate.m_questName) != null) {
                throw new InvalidOperationException($"Cannot rename quest '{existing.m_questName}': a quest named '{questTemplate.m_questName}' already exists.");
            }
            // Renaming the stored record first makes the store move it in place, so the quest keeps its source file.
            existing.m_questName = questTemplate.m_questName;
            await store.SaveAsync(existing);
        }
        await store.SaveAsync(WorldRecordCopy.Detached(questTemplate));
        await SaveMetadataAsync(store, metadataId, questTemplate.m_questName, name, description);
        return true;
    }

    public static async Task<QuestDocument?> GetQuestAsync(string questId) {
        var store = WorldStoreProvider.Current;
        var template = await store.GetAsync<QuestTemplate>(QuestNameOf(questId));
        if (template == null) {
            return null;
        }

        return ToDocument(WorldRecordCopy.Detached(template), await store.GetAsync<QuestMetadata>(TemplateIdOf(template.m_questName)));
    }

    /// <returns>All quests, most recently modified first</returns>
    public static async Task<List<QuestDocument>> GetAllQuestsAsync() {
        var store = WorldStoreProvider.Current;
        var documents = new List<QuestDocument>();
        foreach (var template in await store.GetAllAsync<QuestTemplate>()) {
            documents.Add(ToDocument(WorldRecordCopy.Detached(template), await store.GetAsync<QuestMetadata>(TemplateIdOf(template.m_questName))));
        }
        return documents.OrderByDescending(q => q.ModifiedAt).ToList();
    }

    /// <summary>
    /// Finds quests whose name starts with, or whose quest name or description contains, the search term
    /// </summary>
    public static async Task<List<QuestDocument>> SearchQuestsAsync(string searchTerm) {
        var quests = await GetAllQuestsAsync();
        return quests.Where(q =>
            q.Name.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase)
            || q.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || q.Template.m_questName.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Finds a quest by its metadata name or, without metadata, its m_questName, ignoring case
    /// </summary>
    public static async Task<QuestDocument?> FindQuestByNameAsync(string questName) {
        var quests = await GetAllQuestsAsync();
        return quests.FirstOrDefault(q => string.Equals(q.Name, questName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Saves a quest template and its metadata under the template's m_questName, updating the quest
    /// that has it or creating a new one. A template without m_questName is given <paramref name="name"/>.
    /// </summary>
    /// <returns>The ID of the saved quest</returns>
    public static async Task<string?> UpsertQuestAsync(QuestTemplate questTemplate, string name, string description = "") {
        var store = WorldStoreProvider.Current;
        if (questTemplate.m_questName.IsEmpty) {
            questTemplate.m_questName = name;
        }

        await store.SaveAsync(WorldRecordCopy.Detached(questTemplate));

        var questId = TemplateIdOf(questTemplate.m_questName);
        await SaveMetadataAsync(store, questId, questTemplate.m_questName, name, description);
        return questId;
    }

    /// <returns>False if no quest has <paramref name="questId"/></returns>
    public static async Task<bool> DeleteQuestAsync(string questId) {
        var store = WorldStoreProvider.Current;
        var questName = QuestNameOf(questId);
        if (await store.GetAsync<QuestTemplate>(questName) == null) {
            return false;
        }

        await store.DeleteAsync<QuestTemplate>(questName);
        if (await store.GetAsync<QuestMetadata>(TemplateIdOf(questName)) != null) {
            await store.DeleteAsync<QuestMetadata>(TemplateIdOf(questName));
        }
        return true;
    }

    private static async Task SaveMetadataAsync(IWorldStore store, string metadataId, string questName, string name, string description) {
        var metadata = await store.GetAsync<QuestMetadata>(metadataId) ?? new QuestMetadata();
        metadata.QuestTemplateId = TemplateIdOf(questName);
        metadata.Name = name;
        metadata.Description = description;
        metadata.ModifiedAt = DateTime.UtcNow;
        metadata.ModifiedBy = Environment.UserName;
        await store.SaveAsync(metadata);
    }

    private static QuestDocument ToDocument(QuestTemplate template, QuestMetadata? metadata) => new() {
        Id = TemplateIdOf(template.m_questName),
        Name = metadata?.Name ?? template.m_questName,
        Description = metadata?.Description ?? "",
        Template = template,
        CreatedAt = metadata?.CreatedAt ?? DateTime.UtcNow,
        ModifiedAt = metadata?.ModifiedAt ?? DateTime.UtcNow,
        CreatedBy = metadata?.CreatedBy ?? Environment.UserName,
        ModifiedBy = metadata?.ModifiedBy ?? Environment.UserName
    };

    private static string QuestNameOf(string questId)
        => questId.StartsWith(TemplateIdPrefix, StringComparison.OrdinalIgnoreCase) ? questId[TemplateIdPrefix.Length..] : questId;

    private static string TemplateIdOf(string questName) => TemplateIdPrefix + questName;
}
