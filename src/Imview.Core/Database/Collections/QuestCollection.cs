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
        var store = WorldStoreProvider.Current;
        var existing = await store.GetAsync<QuestTemplate>(QuestNameOf(questId));
        if (existing == null) {
            return false;
        }

        CopyQuestTemplateProperties(questTemplate, existing);
        await store.SaveAsync(existing);
        await SaveMetadataAsync(store, TemplateIdOf(QuestNameOf(questId)), existing, name, description);
        return true;
    }

    public static async Task<QuestDocument?> GetQuestAsync(string questId) {
        var store = WorldStoreProvider.Current;
        var template = await store.GetAsync<QuestTemplate>(QuestNameOf(questId));
        if (template == null) {
            return null;
        }

        return ToDocument(template, await store.GetAsync<QuestMetadata>(TemplateIdOf(template.m_questName)));
    }

    /// <returns>All quests, most recently modified first</returns>
    public static async Task<List<QuestDocument>> GetAllQuestsAsync() {
        var store = WorldStoreProvider.Current;
        var documents = new List<QuestDocument>();
        foreach (var template in await store.GetAllAsync<QuestTemplate>()) {
            documents.Add(ToDocument(template, await store.GetAsync<QuestMetadata>(TemplateIdOf(template.m_questName))));
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

        var existing = await store.GetAsync<QuestTemplate>(questTemplate.m_questName);
        if (existing != null) {
            CopyQuestTemplateProperties(questTemplate, existing);
        }
        var saved = existing ?? questTemplate;
        await store.SaveAsync(saved);

        var questId = TemplateIdOf(saved.m_questName);
        await SaveMetadataAsync(store, questId, saved, name, description);
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

    private static async Task SaveMetadataAsync(IWorldStore store, string metadataId, QuestTemplate template, string name, string description) {
        var metadata = await store.GetAsync<QuestMetadata>(metadataId) ?? new QuestMetadata();
        metadata.QuestTemplateId = TemplateIdOf(template.m_questName);
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

    /// <summary>
    /// Copies all properties from source QuestTemplate to target QuestTemplate
    /// </summary>
    /// <param name="source">Source template to copy from</param>
    /// <param name="target">Target template to copy to</param>
    internal static void CopyQuestTemplateProperties(QuestTemplate source, QuestTemplate target) {
        target.m_questName = source.m_questName;
        target.m_questNameID = source.m_questNameID;
        target.m_questTitle = source.m_questTitle;
        target.m_questInfo = source.m_questInfo;
        target.m_questPrep = source.m_questPrep;
        target.m_questUnderway = source.m_questUnderway;
        target.m_questComplete = source.m_questComplete;
        target.m_startGoals = source.m_startGoals;
        target.m_goals = source.m_goals;
        target.m_startResults = source.m_startResults;
        target.m_endResults = source.m_endResults;
        target.m_requirements = source.m_requirements;
        target.m_prepRequirements = source.m_prepRequirements;
        target.m_pruneRequirements = source.m_pruneRequirements;
        target.m_prepAlways = source.m_prepAlways;
        target.m_clientTags = source.m_clientTags;
        target.m_goalLogic = source.m_goalLogic;
        target.m_questLevel = source.m_questLevel;
        target.m_questRepeat = source.m_questRepeat;
        target.m_onStartQuestScript = source.m_onStartQuestScript;
        target.m_onEndQuestScript = source.m_onEndQuestScript;
        target.m_dialogList = source.m_dialogList;
        target.m_missionDoors = source.m_missionDoors;
        target.m_dynaMods = source.m_dynaMods;
        target.m_isHidden = source.m_isHidden;
        target.m_outdated = source.m_outdated;
        target.m_noQuestHelper = source.m_noQuestHelper;
        target.m_mainline = source.m_mainline;
        target.m_defaultDialogAnimation = source.m_defaultDialogAnimation;
        target.m_skipQHAutoSelect = source.m_skipQHAutoSelect;
        target.m_questEffectInfoList = source.m_questEffectInfoList;
        target.m_forceInteraction = source.m_forceInteraction;
        target.m_checkInventoryForCrafting = source.m_checkInventoryForCrafting;
        target.m_playAsYourPetNPC = source.m_playAsYourPetNPC;
        target.m_activityType = source.m_activityType;
    }
}
