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

namespace Imview.Core.Common;

/// <summary>
/// Manages configuration settings by reading and parsing INI configuration files.
/// </summary>
public static class ConfigurationManager {

    private static readonly Dictionary<string, string> s_settings = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Dictionary<string, string>> s_sections = new(StringComparer.OrdinalIgnoreCase);
    private static bool s_isInitialized = false;
    private static string s_configFilePath = "";
    private static readonly char[] s_lineBreakChars = ['\r', '\n'];
    private static readonly char[] s_reservedKeyChars = ['[', ']', '=', '\r', '\n'];

    /// <summary>
    /// Provides indexer access to configuration settings.
    /// </summary>
    public static ConfigSettings Settings { get; } = new();

    /// <summary>
    /// Initializes the configuration manager with the specified INI file.
    /// </summary>
    /// <param name="iniFilePath">Path to the INI file</param>
    public static void Initialize(string iniFilePath) {
        if (!File.Exists(iniFilePath)) {
            // Create a default config file if it doesn't exist
            CreateDefaultConfig(iniFilePath);
        }

        s_configFilePath = iniFilePath;
        LoadConfiguration();
        s_isInitialized = true;
    }

    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value.</returns>
    public static string? GetSetting(string key) {
        EnsureInitialized();

        if (s_settings.TryGetValue(key, out string? value)) {
            return value;
        }

        // If the key contains a period, it might be in the format "section.key".
        if (key.Contains('.')) {
            var parts = key.Split(['.'], 2);
            var section = parts[0];
            var sectionKey = parts[1];

            if (s_sections.TryGetValue(section, out var sectionSettings)
                && sectionSettings.TryGetValue(sectionKey, out value!)) {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Sets a configuration value by key and saves to file.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The configuration value.</param>
    public static void SetSetting(string key, string value) {
        EnsureInitialized();

        if (key.IndexOfAny(s_reservedKeyChars) >= 0) {
            throw new ArgumentException($"Configuration key {key} contains one of '[', ']', '=' or a line break.", nameof(key));
        }

        if (value.IndexOfAny(s_lineBreakChars) >= 0) {
            throw new ArgumentException($"Configuration value for {key} contains a line break.", nameof(value));
        }

        if (key.Contains('.')) {
            var parts = key.Split(['.'], 2);
            var section = parts[0];
            var sectionKey = parts[1];

            if (!s_sections.TryGetValue(section, out var sectionSettings)) {
                sectionSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                s_sections[section] = sectionSettings;
            }

            sectionSettings[sectionKey] = value;
            s_settings[key] = value;
        } else {
            s_settings[key] = value;
        }

        SaveConfiguration();
    }

    /// <summary>
    /// Gets a section from the configuration
    /// </summary>
    /// <param name="sectionName">The section name</param>
    /// <returns>Dictionary containing all keys and values in the section</returns>
    public static Dictionary<string, string> GetSection(string sectionName) {
        EnsureInitialized();

        if (s_sections.TryGetValue(sectionName, out var section)) {
            return new Dictionary<string, string>(section);
        }

        return [];
    }

    /// <summary>
    /// Gets all section names from the configuration
    /// </summary>
    /// <returns>List of section names</returns>
    public static List<string> GetSectionNames() {
        EnsureInitialized();

        return [.. s_sections.Keys];
    }

    /// <summary>
    /// Reloads the configuration from the file
    /// </summary>
    public static void Reload() {
        EnsureInitialized();
        LoadConfiguration();
    }

    /// <summary>
    /// Gets a value as a specific type
    /// </summary>
    /// <typeparam name="T">The type to convert to</typeparam>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultValue">The default value if key not found or conversion fails</param>
    /// <returns>The converted value or default value</returns>
    public static T GetValue<T>(string key, T defaultValue = default!) {
        string value = GetSetting(key) ?? string.Empty;

        if (string.IsNullOrEmpty(value)) {
            return defaultValue;
        }

        try {
            // Handle boolean values specifically for "True" and "False" strings.
            if (typeof(T) == typeof(bool)) {
                return (T) (object) bool.Parse(value);
            }

            // Handle Enum types.
            if (typeof(T).IsEnum) {
                return (T) Enum.Parse(typeof(T), value, true);
            }

            // Convert to the requested type.
            return (T) Convert.ChangeType(value, typeof(T));
        }
        catch {
            return defaultValue;
        }
    }

    private static void CreateDefaultConfig(string iniFilePath) {
        var directory = Path.GetDirectoryName(iniFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        var defaultConfig = @"[ClientFiles]
RevisionsUrl = https://patcher.r10.one/revisions
SelectedRevision = 

[DataPack]
Path = 
";

        File.WriteAllText(iniFilePath, defaultConfig);
    }

    private static void LoadConfiguration() {
        s_settings.Clear();
        s_sections.Clear();

        string? currentSection = null;
        Dictionary<string, string>? currentSectionSettings = null;

        foreach (string line in File.ReadAllLines(s_configFilePath)) {
            string trimmedLine = line.Trim();

            // Skip empty lines and comments.
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";")) {
                continue;
            }

            // Check if this is a section header.
            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]")) {
                currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                if (!s_sections.TryGetValue(currentSection, out currentSectionSettings)) {
                    currentSectionSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    s_sections[currentSection] = currentSectionSettings;
                }

                continue;
            }

            // Parse key-value pair.
            int equalsIndex = trimmedLine.IndexOf('=');
            if (equalsIndex > 0) {
                string key = trimmedLine.Substring(0, equalsIndex).Trim();
                string value = trimmedLine.Substring(equalsIndex + 1).Trim();

                // Store in the appropriate collection.
                if (currentSection != null) {
                    if (currentSectionSettings != null) {
                        currentSectionSettings[key] = value;
                    }

                    // Also store as a fully qualified key for direct access.
                    s_settings[$"{currentSection}.{key}"] = value;
                }
                else {
                    s_settings[key] = value;
                }
            }
        }
    }

    private static void SaveConfiguration() {
        var lines = new List<string>();

        foreach (var section in s_sections) {
            lines.Add($"[{section.Key}]");
            
            foreach (var kvp in section.Value) {
                lines.Add($"{kvp.Key} = {kvp.Value}");
            }
            
            lines.Add(""); // Add blank line between sections
        }

        File.WriteAllLines(s_configFilePath, lines);
    }

    private static void EnsureInitialized() {
        if (!s_isInitialized) {
            throw new InvalidOperationException("ConfigurationManager has not been initialized. Call Initialize() first.");
        }
    }

    /// <summary>
    /// Provides a fluent interface for accessing configuration settings with type conversion.
    /// </summary>
    public class ConfigSettings {

        /// <summary>
        /// Gets a configuration value by key
        /// </summary>
        /// <param name="key">The configuration key</param>
        /// <returns>A ConfigValue that can be converted to various types</returns>
        public ConfigValue this[string key] => new(GetSetting(key)!);

    }

    /// <summary>
    /// Represents a configuration value with flexible type conversion capabilities.
    /// </summary>
    public class ConfigValue {

        private readonly string _value;

        /// <summary>
        /// Initializes a new instance of the ConfigValue class
        /// </summary>
        /// <param name="value">The string value</param>
        public ConfigValue(string value)
            => _value = value;

        /// <summary>
        /// Implicitly converts a ConfigValue to a string
        /// </summary>
        /// <param name="configValue">The ConfigValue to convert</param>
        public static implicit operator string(ConfigValue configValue)
            => configValue._value;

        /// <summary>
        /// Converts the value to a byte
        /// </summary>
        /// <returns>The byte value</returns>
        public byte AsByte()
            => string.IsNullOrEmpty(_value) ? (byte) 0 : Convert.ToByte(_value);

        /// <summary>
        /// Converts the value to a byte with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The byte value or default</returns>
        public byte AsByte(byte defaultValue) {
            try {
                return AsByte();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to an integer
        /// </summary>
        /// <returns>The integer value</returns>
        public int AsInt()
            => string.IsNullOrEmpty(_value) ? 0 : Convert.ToInt32(_value);

        /// <summary>
        /// Converts the value to an integer with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The integer value or default</returns>
        public int AsInt(int defaultValue) {
            try {
                return AsInt();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to a boolean
        /// </summary>
        /// <returns>The boolean value</returns>
        public bool AsBool() {
            if (string.IsNullOrEmpty(_value)) {
                return false;
            }

            return _value.ToLower() switch {
                "true" or "yes" or "1" or "on" or "enabled" => true,
                _ => false
            };
        }

        /// <summary>
        /// Converts the value to a boolean with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The boolean value or default</returns>
        public bool AsBool(bool defaultValue) {
            try {
                return AsBool();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to a string
        /// </summary>
        /// <returns>The string value</returns>
        public string AsString()
            => _value ?? string.Empty;

        /// <summary>
        /// Converts the value to a string with a default if null
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The string value or default</returns>
        public string AsString(string defaultValue)
            => string.IsNullOrEmpty(_value) ? defaultValue : _value;

    }

}