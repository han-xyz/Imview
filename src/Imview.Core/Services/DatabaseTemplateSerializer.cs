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
using System.Threading.Tasks;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Database.Collections;
using Imview.Core.Database.Models;
using Avalonia.Controls;
using Imview.Core.Views;

namespace Imview.Core.Services;

/// <summary>
/// Service for serializing and deserializing quest templates to/from the database.
/// </summary>
public static class DatabaseTemplateSerializer {

    /// <summary>
    /// Saves a quest template to the database.
    /// </summary>
    /// <param name="template">The template to save</param>
    /// <param name="parentWindow">The parent window for dialogs</param>
    /// <returns>A task that completes when the save operation is done, with the quest ID if successful</returns>
    public static async Task<string?> SaveTemplateAsync(QuestTemplate template, Avalonia.Controls.Window parentWindow) {
        ArgumentNullException.ThrowIfNull(template);

        try {
            // Get quest name from template or prompt user
            var questName = template.m_questName.ToString();
            if (string.IsNullOrWhiteSpace(questName)) {
                questName = "New Quest";
            }

            // Create a simple input dialog to get quest name and description
            var inputDialog = new QuestSaveDialog(questName, "");
            var result = await inputDialog.ShowDialog<QuestSaveResult?>(parentWindow);
            
            if (result == null) {
                // User cancelled
                return null;
            }

            // Save template and metadata to separate collections
            var questTemplateId = await QuestTemplateCollection.SaveQuestTemplateAsync(template, result.Name);
            if (questTemplateId == null) {
                return null;
            }

            var metadata = new QuestMetadata {
                QuestTemplateId = questTemplateId,
                Name = result.Name,
                Description = result.Description,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = Environment.UserName,
                ModifiedBy = Environment.UserName
            };

            await QuestMetadataCollection.SaveQuestMetadataAsync(metadata);
            return questTemplateId;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error saving quest template: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Updates an existing quest template in the database.
    /// </summary>
    /// <param name="questId">The ID of the quest to update</param>
    /// <param name="template">The template to save</param>
    /// <param name="parentWindow">The parent window for dialogs</param>
    /// <returns>A task that completes when the update operation is done, with success status</returns>
    public static async Task<bool> UpdateTemplateAsync(string questId, QuestTemplate template, Avalonia.Controls.Window parentWindow) {
        ArgumentNullException.ThrowIfNull(template);

        try {
            // Get existing quest info
            var existingQuest = await QuestCollection.GetQuestAsync(questId);
            if (existingQuest == null) {
                return false;
            }

            // Create a simple input dialog to edit quest name and description
            var inputDialog = new QuestSaveDialog(existingQuest.Name, existingQuest.Description);
            var result = await inputDialog.ShowDialog<QuestSaveResult?>(parentWindow);
            
            if (result == null) {
                // User cancelled
                return false;
            }

            // Update template and metadata in separate collections
            var templateUpdated = await QuestTemplateCollection.UpdateQuestTemplateAsync(template);
            if (!templateUpdated) {
                return false;
            }

            // Update metadata
            var metadata = await QuestMetadataCollection.GetQuestMetadataByTemplateIdAsync(questId);
            if (metadata != null) {
                metadata.Name = result.Name;
                metadata.Description = result.Description;
                metadata.ModifiedAt = DateTime.UtcNow;
                metadata.ModifiedBy = Environment.UserName;
                await QuestMetadataCollection.UpdateQuestMetadataAsync(metadata);
            }

            return true;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error updating quest template: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads a quest template from the database.
    /// </summary>
    /// <param name="questId">The ID of the quest to load</param>
    /// <returns>The quest template or null if not found</returns>
    public static async Task<QuestTemplate?> LoadTemplateAsync(string questId) {
        try {
            return await QuestTemplateCollection.GetQuestTemplateAsync(questId);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error loading quest template: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Result from quest save dialog
/// </summary>
public record QuestSaveResult(string Name, string Description);