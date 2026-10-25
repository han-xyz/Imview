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
using Raven.Client.Documents;

namespace Imview.Core.Database.Collections;

/// <summary>
/// Collection for managing raw QuestTemplate objects without metadata wrapper
/// </summary>
public static class QuestTemplateCollection {

    /// <summary>
    /// Saves a quest template directly to the database
    /// </summary>
    /// <param name="questTemplate">The quest template to save</param>
    /// <param name="questId">Optional specific ID, uses m_questName if not provided</param>
    /// <returns>The ID of the saved quest template</returns>
    public static async Task<string?> SaveQuestTemplateAsync(QuestTemplate questTemplate, string? questId = null) {
        try {
            var store = WorldDatabase.Instance.Store;
            if (store == null) {
                throw new InvalidOperationException("Database connection not available");
            }

            using var session = store.OpenAsyncSession();
            
            // Use provided ID or fall back to quest name
            var id = questId ?? questTemplate.m_questName;
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentException("Quest template must have a name or provided ID");
            }

            // Handle both full document IDs and just quest names
            var documentId = id.StartsWith("questtemplates/") ? id : $"questtemplates/{id}";
            
            await session.StoreAsync(questTemplate, documentId);
            await session.SaveChangesAsync();

            return documentId;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error saving quest template: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Updates an existing quest template in the database
    /// </summary>
    /// <param name="questTemplate">The updated quest template</param>
    /// <returns>True if successful</returns>
    public static async Task<bool> UpdateQuestTemplateAsync(QuestTemplate questTemplate, string? questId = null) {
        try {
            var store = WorldDatabase.Instance.Store;
            if (store == null) {
                throw new InvalidOperationException("Database connection not available");
            }

            using var session = store.OpenAsyncSession();
            
            // For updates, we must use the provided questId to maintain the same document
            if (string.IsNullOrWhiteSpace(questId)) {
                throw new ArgumentException("Quest ID is required for updates");
            }
            
            // Handle both full document IDs and just quest names
            var documentId = questId.StartsWith("questtemplates/") ? questId : $"questtemplates/{questId}";
            
            var existingTemplate = await session.LoadAsync<QuestTemplate>(documentId);
            if (existingTemplate == null) {
                return false;
            }

            // Copy all properties from the new template to the existing one
            CopyQuestTemplateProperties(questTemplate, existingTemplate);
            await session.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error updating quest template (ID: {questId}, Name: {questTemplate.m_questName}): {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Retrieves a quest template by ID
    /// </summary>
    /// <param name="questId">The quest template ID</param>
    /// <returns>The quest template or null if not found</returns>
    public static async Task<QuestTemplate?> GetQuestTemplateAsync(string questId) {
        try {
            var store = WorldDatabase.Instance.Store;
            if (store == null) {
                throw new InvalidOperationException("Database connection not available");
            }

            using var session = store.OpenAsyncSession();
            
            // Ensure proper document ID format
            var documentId = questId.StartsWith("questtemplates/") ? questId : $"questtemplates/{questId}";
            return await session.LoadAsync<QuestTemplate>(documentId);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error retrieving quest template: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Retrieves all quest templates from the database
    /// </summary>
    /// <returns>List of quest templates</returns>
    public static async Task<List<QuestTemplate>> GetAllQuestTemplatesAsync() {
        try {
            var store = WorldDatabase.Instance.Store;
            if (store == null) {
                throw new InvalidOperationException("Database connection not available");
            }

            using var session = store.OpenAsyncSession();
            
            var results = await session.Query<QuestTemplate>()
                .ToListAsync();

            return results;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error retrieving all quest templates: {ex.Message}");
            return new List<QuestTemplate>();
        }
    }

    /// <summary>
    /// Finds a quest template by quest name
    /// </summary>
    /// <param name="questName">The m_questName property value</param>
    /// <returns>The quest template or null if not found</returns>
    public static async Task<QuestTemplate?> FindQuestTemplateByNameAsync(string questName) {
        try {
            var store = WorldDatabase.Instance.Store;
            if (store == null) {
                throw new InvalidOperationException("Database connection not available");
            }

            using var session = store.OpenAsyncSession();
            
            var result = await session.Query<QuestTemplate>()
                .Where(q => q.m_questName == questName)
                .FirstOrDefaultAsync();

            return result;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error finding quest template by name: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Deletes a quest template from the database
    /// </summary>
    /// <param name="questId">The quest template ID to delete</param>
    /// <returns>True if successful</returns>
    public static async Task<bool> DeleteQuestTemplateAsync(string questId) {
        try {
            var store = WorldDatabase.Instance.Store;
            if (store == null) {
                throw new InvalidOperationException("Database connection not available");
            }

            using var session = store.OpenAsyncSession();
            
            // Ensure proper document ID format
            var documentId = questId.StartsWith("questtemplates/") ? questId : $"questtemplates/{questId}";
            var questTemplate = await session.LoadAsync<QuestTemplate>(documentId);
            if (questTemplate == null) {
                return false;
            }

            session.Delete(questTemplate);
            await session.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error deleting quest template: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Copies all properties from source QuestTemplate to target QuestTemplate
    /// </summary>
    /// <param name="source">Source template to copy from</param>
    /// <param name="target">Target template to copy to</param>
    private static void CopyQuestTemplateProperties(QuestTemplate source, QuestTemplate target) {
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