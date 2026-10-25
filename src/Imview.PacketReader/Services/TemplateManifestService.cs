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
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;

namespace Imview.PacketReader.Services;

/// <summary>
/// Service for loading and managing the template manifest from Root.wad.
/// Provides lookup functionality to resolve object names to template IDs.
/// </summary>
public sealed class TemplateManifestService {
    
    private static readonly Lazy<TemplateManifestService> _instance = new(() => new TemplateManifestService());
    public static TemplateManifestService Instance => _instance.Value;
    
    private TemplateManifest? _templateManifest;
    private Dictionary<string, uint>? _objectNameToIdMap;
    private readonly object _lock = new();
    private bool _isLoaded;
    
    private TemplateManifestService() { }
    
    /// <summary>
    /// Gets whether the template manifest is currently loaded.
    /// </summary>
    public bool IsLoaded {
        get {
            lock (_lock) {
                return _isLoaded;
            }
        }
    }
    
    /// <summary>
    /// Gets the number of templates in the manifest, or 0 if not loaded.
    /// </summary>
    public int TemplateCount {
        get {
            lock (_lock) {
                return _templateManifest?.m_serializedTemplates?.Count ?? 0;
            }
        }
    }
    
    /// <summary>
    /// Loads the template manifest from the provided file data.
    /// This method is designed to work with file data obtained from Root.wad.
    /// </summary>
    /// <param name="manifestData">The template manifest file data.</param>
    /// <returns>True if successfully loaded, false if failed to load.</returns>
    public bool LoadFromFileData(Memory<byte> manifestData) {
        lock (_lock) {
            try {
                var serializer = new BindSerializer();
                if (!serializer.Deserialize<TemplateManifest>(manifestData.ToArray(), 1, out var templateManifest)) {
                    Console.WriteLine("Failed to deserialize TemplateManifest.xml.");
                    return false;
                }

                _templateManifest = templateManifest;
                BuildObjectNameMap();
                _isLoaded = true;
                
                Console.WriteLine($"Template manifest loaded successfully: {TemplateCount} templates");
                return true;
            }
            catch (Exception ex) {
                Console.WriteLine($"Failed to load template manifest: {ex.Message}");
                _isLoaded = false;
                return false;
            }
        }
    }
    
    /// <summary>
    /// Gets the template ID for the specified object name.
    /// </summary>
    /// <param name="objectName">The object name to lookup (e.g., "WC-ST01-NPC04")</param>
    /// <returns>The template ID if found, 0 if not found or not loaded.</returns>
    public uint GetTemplateIdByObjectName(string objectName) {
        if (string.IsNullOrEmpty(objectName)) {
            return 0;
        }

        lock (_lock) {
            if (!_isLoaded || _objectNameToIdMap == null) {
                return 0;
            }

            return _objectNameToIdMap.TryGetValue(objectName, out var templateId) ? templateId : 0;
        }
    }
    
    /// <summary>
    /// Gets the template ID for an object name extracted from a persona string.
    /// </summary>
    /// <param name="personaName">The persona name (e.g., "WC-ST01-NPC04_Persona")</param>
    /// <returns>The template ID if found, 0 if not found or not loaded.</returns>
    public uint GetTemplateIdByPersonaName(string personaName) {
        if (string.IsNullOrEmpty(personaName)) {
            return 0;
        }

        // Strip the "_Persona" suffix to get the object name
        var objectName = personaName;
        if (personaName.EndsWith("_Persona", StringComparison.OrdinalIgnoreCase)) {
            objectName = personaName.Substring(0, personaName.Length - "_Persona".Length);
        }

        return GetTemplateIdByObjectName(objectName);
    }
    
    /// <summary>
    /// Gets all template locations for debugging purposes.
    /// </summary>
    /// <returns>A list of template locations, or empty if not loaded.</returns>
    public IEnumerable<TemplateLocation> GetAllTemplateLocations() {
        lock (_lock) {
            return _templateManifest?.m_serializedTemplates?.ToList() ?? Enumerable.Empty<TemplateLocation>();
        }
    }
    
    /// <summary>
    /// Unloads the template manifest from memory.
    /// </summary>
    public void Unload() {
        lock (_lock) {
            _templateManifest = null;
            _objectNameToIdMap?.Clear();
            _objectNameToIdMap = null;
            _isLoaded = false;
        }
    }
    
    /// <summary>
    /// Builds the object name to template ID mapping for fast lookups.
    /// </summary>
    private void BuildObjectNameMap() {
        if (_templateManifest?.m_serializedTemplates == null) {
            return;
        }

        _objectNameToIdMap = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var templateLocation in _templateManifest.m_serializedTemplates) {
            if (templateLocation is null || templateLocation.m_filename.IsEmpty) {
                continue;
            }

            // Extract object name from file path
            // Example: "ObjectData/WC/WC-ST01-NPC04.xml" -> "WC-ST01-NPC04"
            var fileName = Path.GetFileNameWithoutExtension(templateLocation.m_filename);
            if (!string.IsNullOrEmpty(fileName)) {
                _objectNameToIdMap[fileName] = templateLocation.m_id;
            }
        }
        
        Console.WriteLine($"Built object name mapping: {_objectNameToIdMap.Count} entries");
    }
}