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
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Imcodec.Wad;
using Newtonsoft.Json;
using System.Linq;

namespace Imview.Core.Services;

/// <summary>
/// Service for handling KIWAD file operations.
/// </summary>
public static class KIWadFileService {

    private const string DEFAULT_FILE_LOCATION = @"C:\ProgramData\KingsIsle Entertainment\Wizard101\Data\GameData";

    private static readonly List<string> s_deserializableExtensions = ["xml", "bin"];
    private static readonly JsonSerializerSettings s_serializerOptions = new() {
        Formatting = Formatting.Indented,
        Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
    };

    /// <summary>
    /// Unpacks multiple KIWAD archive files to a specified directory.
    /// </summary>
    /// <param name="parentWindow">The parent window for dialogs</param>
    /// <param name="attemptDeserialization">Whether to attempt deserialization of files</param>
    /// <returns>True if the operation succeeded, false otherwise</returns>
    public static async Task<bool> UnpackKiwadArchiveAsync(Window parentWindow, bool attemptDeserialization) {
        try {
            var openOptions = new FilePickerOpenOptions {
                Title = "Select KIWAD Archive(s)",
                AllowMultiple = true,
                FileTypeFilter = [
                    new("KIWAD Archives") { Patterns = ["*.wad"] },
                    new("All Files") { Patterns = ["*.*"] }
                ]
            };

            // Set default directory if it exists.
            if (Directory.Exists(DEFAULT_FILE_LOCATION)) {
                openOptions.SuggestedStartLocation 
                    = await parentWindow.StorageProvider.TryGetFolderFromPathAsync(DEFAULT_FILE_LOCATION);
            }

            var files = await parentWindow.StorageProvider.OpenFilePickerAsync(openOptions);
            if (files == null || files.Count == 0) {
                // User cancelled.
                return false;
            }

            var archivePaths = files
                .Select(f => f.Path.LocalPath)
                .ToList();

            // Validate that all selected files exist.
            foreach (var path in archivePaths) {
                if (!File.Exists(path)) {
                    MessageService.Error($"The specified archive file '{path}' does not exist.")
                        .WithDuration(TimeSpan.FromSeconds(5))
                        .Send();
                    return false;
                }
            }

            // Select output directory.
            var folderOptions = new FolderPickerOpenOptions {
                Title = "Select Output Directory",
                AllowMultiple = false
            };

            var folders = await parentWindow.StorageProvider.OpenFolderPickerAsync(folderOptions);
            if (folders == null || folders.Count == 0) {
                // User cancelled.
                return false;
            }

            var outputPath = folders[0].Path.LocalPath;

            // Begin extraction process..
            MessageService.Info($"Unpacking {archivePaths.Count} archive(s)...")
                .WithDuration(TimeSpan.FromSeconds(3))
                .Send();

            // Process archives in parallel.
            var tasks = archivePaths
                .Select(path => ProcessArchiveAsync(path, outputPath, attemptDeserialization));
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r.Success);
            var totalFiles = results.Sum(r => r.FileCount);

            if (successCount == archivePaths.Count) {
                MessageService.Info($"Successfully extracted {totalFiles} files from {successCount} archive(s).")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();

                return true;
            }
            else if (successCount > 0) {
                MessageService.Warn($"Partially successful: extracted {totalFiles} files from {successCount} of {archivePaths.Count} archive(s).")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();

                return true;
            }
            else {
                MessageService.Error("Failed to extract any archives.")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();

                return false;
            }
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to unpack archives: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();

            return false;
        }
    }

    private static async Task<(bool Success, int FileCount)> ProcessArchiveAsync(string archivePath, string baseOutputPath, bool attemptDeserialization) {
        try {
            // Read the file data and parse it as an archive
            var fileData = await File.ReadAllBytesAsync(archivePath);
            Archive archive;

            using var archiveStream = new MemoryStream(fileData);
            try {
                archive = ArchiveParser.Parse(archiveStream)!;
                if (archive == null) {
                    Console.WriteLine($"Archive is not valid: {archivePath}");

                    return (false, 0);
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Failed to parse archive: {archivePath}: {ex.Message}");

                return (false, 0);
            }

            var archiveName = Path.GetFileNameWithoutExtension(archivePath);

            // Create the output directory if it doesn't exist
            var extractPath = Path.Combine(baseOutputPath, archiveName);
            if (!Directory.Exists(extractPath)) {
                _ = Directory.CreateDirectory(extractPath);
            }

            // Extract files from the archive
            var extractedFiles = UnpackArchiveFiles(archive);
            WriteArchiveFilesToDisk(extractedFiles, extractPath, attemptDeserialization);

            return (true, extractedFiles.Count);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error processing archive {archivePath}: {ex.Message}");

            return (false, 0);
        }
    }

    private static Dictionary<FileEntry, byte[]?> UnpackArchiveFiles(Archive archive) {
        // Files within the archive are lazy loaded. We'll iterate through each file and extract the data.
        var files = new Dictionary<FileEntry, byte[]?>();
        foreach (var entry in archive.Files) {
            var fileData = archive.OpenFile(entry.Key);
            var fileEntry = entry.Value.Value;
            if (fileData == null) {
                continue;
            }

            files.Add(fileEntry, fileData.Value.ToArray());
        }

        return files;
    }

    public static void WriteArchiveFilesToDisk(Dictionary<FileEntry, byte[]?> files,
                                             string outputPath,
                                             bool attemptDeserialization) {
        foreach (var file in files) {
            var fileEntry = file.Key;
            var fileData = file.Value;
            if (fileData == null) {
                continue;
            }

            var fileName = fileEntry.FileName ?? "unknown";
            var fileExt = Path.GetExtension(fileName).TrimStart('.');
            string fileOutputPath;
            try {
                fileOutputPath = CreateFileOutputPath(outputPath, fileName);
            }
            catch (InvalidOperationException) {
                Console.WriteLine($"Warning: skipped archive entry that resolves outside the output folder: {fileName}");
                continue;
            }

            // Either write to the file, or attempt deserialization. If we fail to deserialize, we'll write
            // the bytes to disk.
            if (attemptDeserialization && s_deserializableExtensions.Contains(fileExt)) {
                var deserializedData = TryDeserializeFile(fileName, fileData);
                if (deserializedData != null) {
                    // If we deserialized successfully, remove the existing extension and add the deserialization suffix.
                    fileOutputPath = Path.ChangeExtension(fileOutputPath, null);
                    fileOutputPath = $"{fileOutputPath}_deser.json";
                    File.WriteAllText(fileOutputPath, deserializedData);

                    continue;
                }
            }

            File.WriteAllBytes(fileOutputPath, fileData);
        }
    }

    /// <exception cref="InvalidOperationException">The entry name resolves outside <paramref name="basePath"/>.</exception>
    private static string CreateFileOutputPath(string basePath, string fileName) {
        var fullPath = ClientFileService.ResolveInside(basePath, fileName);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }

    private static string? TryDeserializeFile(string fileName, byte[] fileData) {
        try {
            var bindSerializer = new Imcodec.ObjectProperty.BindSerializer();
            if (bindSerializer.Deserialize<Imcodec.ObjectProperty.PropertyClass>(fileData, out var propertyClass)) {
                // Create wrapper object with metadata.
                var deserializedObject = new {
                    _fileName = fileName,
                    _flags = (uint) bindSerializer.SerializerFlags,
                    _className = propertyClass.GetType().Name,
                    _hash = propertyClass.GetHash(),
                    _deserializedOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    _imcodecVersion = typeof(KIWadFileService).Assembly.GetName()?.Version?.ToString() ?? "Unknown",
                    _object = propertyClass
                };

                return JsonConvert.SerializeObject(deserializedObject, s_serializerOptions);
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Failed to deserialize file ({fileName}): {ex.Message}");
        }

        return null;
    }
}