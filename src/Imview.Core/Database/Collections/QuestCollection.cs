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
using Imview.Core.Database.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Queries;

namespace Imview.Core.Database.Collections;

/// <summary>
/// Represents a quest document stored in RavenDB with metadata
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
/// Collection for managing quest templates in the WorldDB database
/// </summary>
public static class QuestCollection {

    /// <summary>
    /// Saves a quest template to the database using the new decoupled architecture
    /// </summary>
    /// <param name="questTemplate">The quest template to save</param>
    /// <param name="name">The name for the quest</param>
    /// <param name="description">Optional description</param>
    /// <returns>The ID of the saved quest template</returns>
    public static async Task<string?> SaveQuestAsync(QuestTemplate questTemplate, string name, string description = "") {
        try {
            // Save template to QuestTemplateCollection
            var questTemplateId = await QuestTemplateCollection.SaveQuestTemplateAsync(questTemplate, name);
            if (questTemplateId == null) {
                return null;
            }

            // Save metadata to QuestMetadataCollection
            var metadata = new QuestMetadata {
                QuestTemplateId = questTemplateId,
                Name = name,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = Environment.UserName,
                ModifiedBy = Environment.UserName
            };

            await QuestMetadataCollection.SaveQuestMetadataAsync(metadata);
            return questTemplateId;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error saving quest: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Updates an existing quest template in the database using the new decoupled architecture
    /// </summary>
    /// <param name="questId">The ID of the quest to update</param>
    /// <param name="questTemplate">The updated quest template</param>
    /// <param name="name">The updated name</param>
    /// <param name="description">The updated description</param>
    /// <returns>True if successful</returns>
    public static async Task<bool> UpdateQuestAsync(string questId, QuestTemplate questTemplate, string name, string description = "") {
        try {
            // Always update template at the original questId location (don't create new one)
            var templateUpdated = await QuestTemplateCollection.UpdateQuestTemplateAsync(questTemplate, questId);
            if (!templateUpdated) {
                return false;
            }

            // Update metadata in QuestMetadataCollection
            var metadata = await QuestMetadataCollection.GetQuestMetadataByTemplateIdAsync(questId);
            if (metadata != null) {
                metadata.Name = name;
                metadata.Description = description;
                metadata.ModifiedAt = DateTime.UtcNow;
                metadata.ModifiedBy = Environment.UserName;
                var metadataUpdated = await QuestMetadataCollection.UpdateQuestMetadataAsync(metadata);
                if (!metadataUpdated) {
                    Console.WriteLine($"Warning: Failed to update metadata for quest {questId}");
                }
            } else {
                // Create metadata if it doesn't exist
                var newMetadata = new QuestMetadata {
                    QuestTemplateId = questId,
                    Name = name,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    CreatedBy = Environment.UserName,
                    ModifiedBy = Environment.UserName
                };
                await QuestMetadataCollection.SaveQuestMetadataAsync(newMetadata);
            }

            return true;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error updating quest: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Retrieves a quest template by ID (backward compatibility wrapper)
    /// </summary>
    /// <param name="questId">The quest ID</param>
    /// <returns>The quest document or null if not found</returns>
    public static async Task<QuestDocument?> GetQuestAsync(string questId) {
        try {
            // Get template from QuestTemplateCollection
            var template = await QuestTemplateCollection.GetQuestTemplateAsync(questId);
            if (template == null) {
                return null;
            }

            // Get metadata from QuestMetadataCollection
            var metadata = await QuestMetadataCollection.GetQuestMetadataByTemplateIdAsync(questId);
            
            // Reconstruct QuestDocument for backward compatibility
            return new QuestDocument {
                Id = questId,
                Name = metadata?.Name ?? template.m_questName,
                Description = metadata?.Description ?? "",
                Template = template,
                CreatedAt = metadata?.CreatedAt ?? DateTime.UtcNow,
                ModifiedAt = metadata?.ModifiedAt ?? DateTime.UtcNow,
                CreatedBy = metadata?.CreatedBy ?? Environment.UserName,
                ModifiedBy = metadata?.ModifiedBy ?? Environment.UserName
            };
        }
        catch (Exception ex) {
            Console.WriteLine($"Error retrieving quest: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Retrieves all quest templates from the database (backward compatibility wrapper)
    /// </summary>
    /// <returns>List of quest documents</returns>
    public static async Task<List<QuestDocument>> GetAllQuestsAsync() {
        try {
            // Get all templates and metadata
            var templates = await QuestTemplateCollection.GetAllQuestTemplatesAsync();
            var allMetadata = await QuestMetadataCollection.GetAllQuestMetadataAsync();
            
            // Create a lookup for metadata by template ID
            var metadataLookup = allMetadata.ToDictionary(m => m.QuestTemplateId, m => m);
            
            // Reconstruct QuestDocuments for backward compatibility
            var results = templates.Select(template => {
                var templateId = $"questtemplates/{template.m_questName}";
                metadataLookup.TryGetValue(templateId, out var metadata);
                
                return new QuestDocument {
                    Id = templateId,
                    Name = metadata?.Name ?? template.m_questName,
                    Description = metadata?.Description ?? "",
                    Template = template,
                    CreatedAt = metadata?.CreatedAt ?? DateTime.UtcNow,
                    ModifiedAt = metadata?.ModifiedAt ?? DateTime.UtcNow,
                    CreatedBy = metadata?.CreatedBy ?? Environment.UserName,
                    ModifiedBy = metadata?.ModifiedBy ?? Environment.UserName
                };
            }).OrderByDescending(q => q.ModifiedAt).ToList();

            return results;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error retrieving all quests: {ex.Message}");
            return new List<QuestDocument>();
        }
    }

    /// <summary>
    /// Searches for quest templates by name (backward compatibility wrapper)
    /// </summary>
    /// <param name="searchTerm">The search term</param>
    /// <returns>List of matching quest documents</returns>
    public static async Task<List<QuestDocument>> SearchQuestsAsync(string searchTerm) {
        try {
            // Search metadata first (contains user-friendly names)
            var matchingMetadata = await QuestMetadataCollection.SearchQuestMetadataAsync(searchTerm);
            var results = new List<QuestDocument>();
            var foundTemplateIds = new HashSet<string>();

            // Add quests that have matching metadata
            foreach (var metadata in matchingMetadata) {
                var template = await QuestTemplateCollection.GetQuestTemplateAsync(metadata.QuestTemplateId);
                if (template != null) {
                    foundTemplateIds.Add(metadata.QuestTemplateId);
                    results.Add(new QuestDocument {
                        Id = metadata.QuestTemplateId,
                        Name = metadata.Name,
                        Description = metadata.Description,
                        Template = template,
                        CreatedAt = metadata.CreatedAt,
                        ModifiedAt = metadata.ModifiedAt,
                        CreatedBy = metadata.CreatedBy,
                        ModifiedBy = metadata.ModifiedBy
                    });
                }
            }

            // Also search quest templates directly for those without metadata
            var allTemplates = await QuestTemplateCollection.GetAllQuestTemplatesAsync();
            foreach (var template in allTemplates) {
                var templateId = $"questtemplates/{template.m_questName}";
                
                // Skip if already found through metadata search
                if (foundTemplateIds.Contains(templateId)) {
                    continue;
                }
                
                // Check if template name matches search term
                if (template.m_questName.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) {
                    results.Add(new QuestDocument {
                        Id = templateId,
                        Name = template.m_questName,
                        Description = "",
                        Template = template,
                        CreatedAt = DateTime.UtcNow,
                        ModifiedAt = DateTime.UtcNow,
                        CreatedBy = Environment.UserName,
                        ModifiedBy = Environment.UserName
                    });
                }
            }

            return results.OrderByDescending(q => q.ModifiedAt).ToList();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error searching quests: {ex.Message}");
            return new List<QuestDocument>();
        }
    }

    /// <summary>
    /// Finds a quest by name (backward compatibility wrapper)
    /// </summary>
    /// <param name="questName">The name of the quest to find</param>
    /// <returns>The quest document or null if not found</returns>
    public static async Task<QuestDocument?> FindQuestByNameAsync(string questName) {
        try {
            // Search metadata by name
            var metadata = await QuestMetadataCollection.FindQuestMetadataByNameAsync(questName);
            if (metadata == null) {
                return null;
            }

            // Get corresponding template
            var template = await QuestTemplateCollection.GetQuestTemplateAsync(metadata.QuestTemplateId);
            if (template == null) {
                return null;
            }

            // Reconstruct QuestDocument
            return new QuestDocument {
                Id = metadata.QuestTemplateId,
                Name = metadata.Name,
                Description = metadata.Description,
                Template = template,
                CreatedAt = metadata.CreatedAt,
                ModifiedAt = metadata.ModifiedAt,
                CreatedBy = metadata.CreatedBy,
                ModifiedBy = metadata.ModifiedBy
            };
        }
        catch (Exception ex) {
            Console.WriteLine($"Error finding quest by name: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Upserts a quest template - creates new if not exists, updates if exists
    /// </summary>
    /// <param name="questTemplate">The quest template</param>
    /// <param name="name">The name for the quest</param>
    /// <param name="description">Optional description</param>
    /// <returns>The ID of the upserted quest document</returns>
    public static async Task<string?> UpsertQuestAsync(QuestTemplate questTemplate, string name, string description = "") {
        try {
            // Check if metadata exists by name
            var existingMetadata = await QuestMetadataCollection.FindQuestMetadataByNameAsync(name);
            
            if (existingMetadata != null) {
                // Update existing quest
                var templateUpdated = await QuestTemplateCollection.UpdateQuestTemplateAsync(questTemplate, existingMetadata.QuestTemplateId);
                if (!templateUpdated) {
                    return null;
                }

                // Update metadata
                existingMetadata.Name = name;
                existingMetadata.Description = description;
                existingMetadata.ModifiedAt = DateTime.UtcNow;
                existingMetadata.ModifiedBy = Environment.UserName;
                await QuestMetadataCollection.UpdateQuestMetadataAsync(existingMetadata);
                
                return existingMetadata.QuestTemplateId;
            } else {
                // Create new quest
                var questTemplateId = await QuestTemplateCollection.SaveQuestTemplateAsync(questTemplate, name);
                if (questTemplateId == null) {
                    return null;
                }

                var metadata = new QuestMetadata {
                    QuestTemplateId = questTemplateId,
                    Name = name,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    CreatedBy = Environment.UserName,
                    ModifiedBy = Environment.UserName
                };

                await QuestMetadataCollection.SaveQuestMetadataAsync(metadata);
                return questTemplateId;
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error upserting quest: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Deletes a quest template and its metadata from the database
    /// </summary>
    /// <param name="questId">The quest ID to delete</param>
    /// <returns>True if successful</returns>
    public static async Task<bool> DeleteQuestAsync(string questId) {
        try {
            // Delete from both collections
            var templateDeleted = await QuestTemplateCollection.DeleteQuestTemplateAsync(questId);
            
            // Find and delete corresponding metadata
            var metadata = await QuestMetadataCollection.GetQuestMetadataByTemplateIdAsync(questId);
            if (metadata != null) {
                await QuestMetadataCollection.DeleteQuestMetadataAsync(metadata.Id);
            }

            return templateDeleted;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error deleting quest: {ex.Message}");
            return false;
        }
    }

}