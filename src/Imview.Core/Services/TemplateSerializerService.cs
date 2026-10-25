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
using System.IO;
using System.Threading.Tasks;
using Imcodec.ObjectProperty.TypeCache;
using System.Collections.Generic;
using Avalonia.Platform.Storage;
using System.Text.Json;
using Imcodec.ObjectProperty;
using Imview.Core.Database.Collections;
using Imview.Core.Views;
using Imview.Core.Common.Extensions;

namespace Imview.Core.Services;

/// <summary>
/// Service for serializing and deserializing quest templates to/from .view files.
/// </summary>
public static class TemplateSerializer {

    private const string VIEW_FILE_EXTENSION = ".view";

    /// <summary>
    /// Shows save options dialog and saves the quest template according to user choice.
    /// </summary>
    /// <param name="template">The template to save</param>
    /// <param name="parentWindow">The parent window for the save dialog</param>
    /// <returns>A task that completes when the save operation is done, with a bool indicating success</returns>
    public static async Task<bool> SaveTemplateAsync(QuestTemplate template, Avalonia.Controls.Window parentWindow) {
        ArgumentNullException.ThrowIfNull(template);

        // Show save options dialog
        var optionsDialog = new QuestSaveOptionsDialog();
        var selectedOption = await optionsDialog.ShowDialog<QuestSaveOption>(parentWindow);
        
        return selectedOption switch {
            QuestSaveOption.SaveLocal => await SaveTemplateToFileAsync(template, parentWindow),
            QuestSaveOption.Upload => await SaveTemplateToDatabase(template, parentWindow),
            QuestSaveOption.SaveBoth => await SaveTemplateToBoth(template, parentWindow),
            _ => false // User cancelled
        };
    }

    /// <summary>
    /// Saves a quest template to the database.
    /// </summary>
    private static async Task<bool> SaveTemplateToDatabase(QuestTemplate template, Avalonia.Controls.Window parentWindow) {
        var questId = await DatabaseTemplateSerializer.SaveTemplateAsync(template, parentWindow);
        return !string.IsNullOrEmpty(questId);
    }

    /// <summary>
    /// Saves a quest template to both local file and database.
    /// </summary>
    private static async Task<bool> SaveTemplateToBoth(QuestTemplate template, Avalonia.Controls.Window parentWindow) {
        var localSuccess = await SaveTemplateToFileAsync(template, parentWindow);
        var databaseSuccess = await SaveTemplateToDatabase(template, parentWindow);
        
        return localSuccess || databaseSuccess; // Success if either works
    }

    /// <summary>
    /// Saves a quest template to a .view file (legacy file-based saving).
    /// </summary>
    /// <param name="template">The template to save</param>
    /// <param name="parentWindow">The parent window for the save dialog</param>
    /// <returns>A task that completes when the save operation is done, with a bool indicating success</returns>
    public static async Task<bool> SaveTemplateToFileAsync(QuestTemplate template, Avalonia.Controls.Window parentWindow) {
        ArgumentNullException.ThrowIfNull(template);

        try {
            // Create save file dialog using StorageProvider API.
            var options = new FilePickerSaveOptions {
                Title = "Save Quest Template",
                SuggestedFileName = SanitizeFileName(template.m_questName.OrIfEmpty("NewQuest")) + VIEW_FILE_EXTENSION,
                FileTypeChoices = [
                    new("Imview Template Files") {
                        Patterns = ["*.view"]
                    }
                ]
            };

            var fileResult = await parentWindow.StorageProvider.SaveFilePickerAsync(options);
            if (fileResult == null) {
                // User cancelled.
                return false;
            }

            var filePath = fileResult.Path.LocalPath;

            // Serialize the template to the selected file.
            await Task.Run(() => {
                // Create the necessary directories if they don't exist.
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                    _ = Directory.CreateDirectory(directory);
                }

                // Write serialized file.
                var serializer = new ObjectSerializer(false);
                if (!serializer.Serialize(template, 1, out var data)) {
                    throw new Exception("Failed to serialize template data.");
                }

                File.WriteAllBytes(filePath, data);
            });

            return true;
        }
        catch {
            return false;
        }
    }
    public static async Task<QuestTemplate?> LoadTemplateAsync(Avalonia.Controls.Window parentWindow) {
        try {
            // Create open file dialog using StorageProvider API.
            var options = new FilePickerOpenOptions {
                Title = "Open Quest Template",
                FileTypeFilter = [
                    new("Imview Template Files") {
                        Patterns = ["*.view"]
                    }
                ]
            };

            var fileResult = await parentWindow.StorageProvider.OpenFilePickerAsync(options);
            if (fileResult == null || fileResult.Count == 0) {
                // User cancelled.
                return null;
            }

            var filePath = fileResult[0].Path.LocalPath;

            // Deserialize the template from the selected file.
            var serializer = new ObjectSerializer(false);
            var data = File.ReadAllBytes(filePath);
            
            return !serializer.Deserialize<QuestTemplate>(data, 1, out var template) ? null : template;
        }
        catch {
            throw;
        }
    }

    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    private static string SanitizeFileName(string fileName) {
        var invalidChars = Path.GetInvalidFileNameChars();

        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries))
            .Replace(" ", "_");
    }

}