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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Database.Collections;
using Imview.Core.Services;
using Imview.Core.Views;
using Imview.Core.Common.Extensions;

namespace Imview.Core.Services;

/// <summary>
/// Service for saving multiple quest templates to a zip file.
/// </summary>
public static class QuestZipService {

    /// <summary>
    /// Saves multiple quest templates to a zip file.
    /// </summary>
    /// <param name="templates">The collection of templates to save</param>
    /// <param name="parentWindow">The parent window for the save dialog</param>
    /// <returns>A task that completes when the save operation is done, with a bool indicating success</returns>
    public static async Task<bool> SaveQuestsToZipFileAsync(
        IEnumerable<QuestTemplate> templates, 
        Avalonia.Controls.Window parentWindow) {

        ArgumentNullException.ThrowIfNull(templates);

        // Create temporary directory for .view files.
        var tempDir = Path.Combine(Path.GetTempPath(), $"Imview_Export_{Guid.NewGuid()}");
        var tempFiles = new List<string>();

        try {
            // Create save file dialog for zip file.
            var options = new FilePickerSaveOptions {
                Title = "Save All Quest Templates",
                SuggestedFileName = $"Quest_Export_{DateTime.Now:yyyyMMdd}.zip",
                FileTypeChoices = [
                    new("Zip Files") {
                        Patterns = ["*.zip"]
                    }
                ]
            };

            var zipResult = await parentWindow.StorageProvider.SaveFilePickerAsync(options);
            if (zipResult == null) {
                // User cancelled.
                return false;
            }

            var zipPath = zipResult.Path.LocalPath;

            _ = Directory.CreateDirectory(tempDir);

            // Serialize each template to a .view file in the temp directory.
            var serializer = new ObjectSerializer(false);
            var questCounter = 0;
            
            foreach (var template in templates) {
                questCounter++;
                var questName = SanitizeFileName(template.m_questName.OrIfEmpty(template.m_questTitle.OrIfEmpty($"Quest_{questCounter}")));
                
                var tempFilePath = Path.Combine(tempDir, $"{questName}.view");
                tempFiles.Add(tempFilePath);
                
                // Serialize the template.
                if (!serializer.Serialize(template, 1, out var data)) {
                    MessageService.Error($"Failed to serialize template: {questName}")
                        .WithDuration(TimeSpan.FromSeconds(3))
                        .Send();

                    continue;
                }
                
                // Write the serialized data to the temp file.
                await File.WriteAllBytesAsync(tempFilePath, data);
            }
            
            // Create the zip file.
            if (File.Exists(zipPath)) {
                File.Delete(zipPath);
            }
            
            ZipFile.CreateFromDirectory(tempDir, zipPath);
            
            return true;
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to save templates to zip file: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
            return false;
        }
        finally {
            // Clean up temp files.
            foreach (var file in tempFiles) {
                try {
                    if (File.Exists(file)) {
                        File.Delete(file);
                    }
                }
                catch {
                    // Ignore clean-up errors.
                }
            }
            
            // Clean up temp directory.
            try {
                if (Directory.Exists(tempDir)) {
                    Directory.Delete(tempDir, true);
                }
            }
            catch {
                // Ignore clean-up errors.
            }
        }
    }
    
    /// <summary>
    /// Saves multiple quest templates to the database.
    /// </summary>
    /// <param name="templates">The collection of templates to save</param>
    /// <param name="parentWindow">The parent window for dialogs</param>
    /// <returns>A task that completes when the save operation is done, with a bool indicating success</returns>
    public static async Task<bool> SaveQuestsToDatabaseAsync(
        IEnumerable<QuestTemplate> templates,
        Avalonia.Controls.Window parentWindow) {

        ArgumentNullException.ThrowIfNull(templates);

        var questList = templates.ToList();
        if (questList.Count == 0) {
            MessageService.Error("No quests to save.")
                .WithDuration(TimeSpan.FromSeconds(3))
                .Send();
            return false;
        }

        try {
            int successCount = 0;
            int totalCount = questList.Count;
            var failedQuests = new List<string>();

            foreach (var template in questList) {
                try {
                    // Generate a descriptive name for the quest
                    var questName = template.m_questName.OrIfEmpty(template.m_questTitle.OrIfEmpty($"Quest_FromPacket_{DateTime.Now:HHmmss}"));

                    var description = $"Quest imported from packet capture on {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                    // Use UpsertQuestAsync to handle duplicates gracefully
                    var questId = await QuestCollection.UpsertQuestAsync(template, questName, description);

                    if (!string.IsNullOrEmpty(questId)) {
                        successCount++;
                        
                        // Show progress for every 5th quest or the last quest
                        if (successCount % 5 == 0 || successCount == totalCount) {
                            MessageService.Info($"Uploaded {successCount}/{totalCount} quests...")
                                .WithDuration(TimeSpan.FromSeconds(2))
                                .Send();
                        }
                    } else {
                        failedQuests.Add(questName);
                    }
                }
                catch (Exception ex) {
                    var questName = template.m_questName.OrIfEmpty("Unknown Quest");
                    failedQuests.Add($"{questName} ({ex.Message})");
                    Console.WriteLine($"Error saving quest {questName}: {ex.Message}");
                }
            }

            // Report results
            if (successCount == totalCount) {
                MessageService.Info($"Successfully uploaded all {totalCount} quests to database.")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();
                return true;
            } else if (successCount > 0) {
                var failedDetails = string.Join(", ", failedQuests.Take(3));
                if (failedQuests.Count > 3) {
                    failedDetails += $" and {failedQuests.Count - 3} more";
                }
                
                MessageService.Warn($"Uploaded {successCount}/{totalCount} quests. Failed: {failedDetails}")
                    .WithDuration(TimeSpan.FromSeconds(8))
                    .Send();
                return true; // Partial success
            } else {
                MessageService.Error("Failed to upload any quests to database.")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();
                return false;
            }
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to upload quests to database: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
            return false;
        }
    }

    /// <summary>
    /// Saves a single quest template with save option dialog.
    /// </summary>
    /// <param name="template">The template to save</param>
    /// <param name="parentWindow">The parent window for dialogs</param>
    /// <returns>A task that completes when the save operation is done, with a bool indicating success</returns>
    public static async Task<bool> SaveSingleQuestAsync(
        QuestTemplate template, 
        Avalonia.Controls.Window parentWindow) {

        ArgumentNullException.ThrowIfNull(template);

        try {
            // Show save options dialog
            var optionsDialog = new QuestSaveOptionsDialog();
            var selectedOption = await optionsDialog.ShowDialog<QuestSaveOption>(parentWindow);
            
            var success = selectedOption switch {
                QuestSaveOption.SaveLocal => await SaveSingleQuestLocal(template, parentWindow),
                QuestSaveOption.Upload => await SaveSingleQuestToDatabase(template, parentWindow),
                QuestSaveOption.SaveBoth => await SaveSingleQuestBoth(template, parentWindow),
                _ => false // User cancelled
            };

            if (success && selectedOption != QuestSaveOption.None) {
                var questName = template.m_questName.OrIfEmpty(template.m_questTitle.OrIfEmpty("Quest"));
                var message = selectedOption switch {
                    QuestSaveOption.SaveLocal => $"Successfully saved quest '{questName}' to file.",
                    QuestSaveOption.Upload => $"Successfully uploaded quest '{questName}' to database.",
                    QuestSaveOption.SaveBoth => $"Successfully saved quest '{questName}' locally and to database.",
                    _ => ""
                };

                MessageService.Info(message)
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();
            }
            
            return success;
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to save quest: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
            return false;
        }
    }

    private static async Task<bool> SaveSingleQuestLocal(QuestTemplate template, Avalonia.Controls.Window parentWindow) {
        try {
            var questName = SanitizeFileName(template.m_questName.OrIfEmpty(template.m_questTitle.OrIfEmpty($"Quest_{DateTime.Now:HHmmss}")));
            
            // Create save file dialog for .view file.
            var options = new FilePickerSaveOptions {
                Title = "Save Quest Template",
                SuggestedFileName = $"{questName}.view",
                FileTypeChoices = [
                    new("View Files") {
                        Patterns = ["*.view"]
                    }
                ]
            };

            var result = await parentWindow.StorageProvider.SaveFilePickerAsync(options);
            if (result == null) {
                return false; // User cancelled
            }

            var filePath = result.Path.LocalPath;
            
            // Serialize the template
            var serializer = new ObjectSerializer(false);
            if (!serializer.Serialize(template, 1, out var data)) {
                MessageService.Error($"Failed to serialize quest template.")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
                return false;
            }
            
            // Write the serialized data to the file
            await File.WriteAllBytesAsync(filePath, data);
            return true;
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to save quest locally: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
            return false;
        }
    }

    private static async Task<bool> SaveSingleQuestToDatabase(QuestTemplate template, Avalonia.Controls.Window parentWindow) {
        try {
            // Generate a descriptive name for the quest
            var questName = template.m_questName.OrIfEmpty(template.m_questTitle.OrIfEmpty($"Quest_FromPacket_{DateTime.Now:HHmmss}"));

            var description = $"Quest imported from packet capture on {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            // Use UpsertQuestAsync to handle duplicates gracefully
            var questId = await QuestCollection.UpsertQuestAsync(template, questName, description);

            return !string.IsNullOrEmpty(questId);
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to upload quest to database: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
            return false;
        }
    }

    private static async Task<bool> SaveSingleQuestBoth(QuestTemplate template, Avalonia.Controls.Window parentWindow) {
        var localSuccess = await SaveSingleQuestLocal(template, parentWindow);
        var databaseSuccess = await SaveSingleQuestToDatabase(template, parentWindow);
        
        return localSuccess || databaseSuccess; // Success if either works
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