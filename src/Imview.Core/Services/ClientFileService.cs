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
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using Imview.Core.Common;
using Imview.Core.Models;

namespace Imview.Core.Services;

public class ClientFileService {

    private static readonly Regex s_revisionPattern = new(@"^[A-Za-z0-9._-]+\z");

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;

    public ClientFileService() {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        _cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Imview", "ClientFiles");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<List<string>> FetchRevisionsAsync() {
        try {
            var url = ConfigurationManager.GetSetting("ClientFiles.RevisionsUrl") ?? "https://patcher.r10.one/revisions";
            
            var response = await _httpClient.GetStringAsync(url);
            var revisions = JsonSerializer.Deserialize<List<string?>>(response);
            
            if (revisions == null) {
                return new List<string>();
            }

            var safeRevisions = new List<string>();
            foreach (var revision in revisions) {
                if (IsSafeRevision(revision)) {
                    safeRevisions.Add(revision);
                }
                else {
                    Console.WriteLine($"Dropped unsafe revision from patcher: {JsonSerializer.Serialize(revision)}");
                }
            }

            return safeRevisions
                .OrderByDescending(GetRevisionNumber)
                .ToList();
        }
        catch (Exception ex) {
            throw new InvalidOperationException($"Failed to fetch revisions: {ex.Message}", ex);
        }
    }

    public static bool IsSafeRevision([NotNullWhen(true)] string? revision) {
        return revision is not null && revision != "." && revision != ".." && s_revisionPattern.IsMatch(revision);
    }

    public static string ResolveInside(string root, string relative) {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
            throw new InvalidOperationException($"Refusing path outside the cache directory: {fullPath}");
        }

        return fullPath;
    }

    public async Task<bool> TestConnectionAsync() {
        try {
            await FetchRevisionsAsync();
            return true;
        }
        catch {
            return false;
        }
    }

    public string GetSelectedRevision() {
        return ConfigurationManager.GetSetting("ClientFiles.SelectedRevision") ?? string.Empty;
    }

    public void SetSelectedRevision(string revision) {
        ConfigurationManager.SetSetting("ClientFiles.SelectedRevision", revision);
    }

    public void SetRevisionsUrl(string url) {
        ConfigurationManager.SetSetting("ClientFiles.RevisionsUrl", url);
    }

    public string GetRevisionsUrl() {
        return ConfigurationManager.GetSetting("ClientFiles.RevisionsUrl") ?? "https://patcher.r10.one/revisions";
    }

    private static int GetRevisionNumber(string revision) {
        if (string.IsNullOrEmpty(revision)) return 0;
        
        var parts = revision.Split('_');
        if (parts.Length >= 2 && parts[1].StartsWith('r')) {
            var numberStr = parts[1][1..];
            if (int.TryParse(numberStr, out int number)) {
                return number;
            }
        }
        
        return 0;
    }

    public async Task<LatestFileList> FetchFileListAsync(string revision) {
        try {
            var baseUrl = GetRevisionsUrl().Replace("/revisions", "");
            var fileListUrl = $"{baseUrl}/{revision}/LatestFileList.xml";
            
            var xmlContent = await _httpClient.GetStringAsync(fileListUrl);
            
            var fileList = new LatestFileList();
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlContent);
            
            // The root element is LatestFileList, and each child element represents a file group
            // containing a RECORD element with the actual file data
            var rootNode = xmlDoc.DocumentElement;
            if (rootNode != null) {
                foreach (XmlNode fileGroupNode in rootNode.ChildNodes) {
                    // Skip text nodes and comments
                    if (fileGroupNode.NodeType != XmlNodeType.Element) continue;
                    
                    // Handle Base section specially - only include Root.wad
                    if (fileGroupNode.Name.Equals("Base", StringComparison.OrdinalIgnoreCase)) {
                        var recordNodes = fileGroupNode.SelectNodes("RECORD");
                        if (recordNodes != null) {
                            foreach (XmlNode recordNode in recordNodes) {
                                var fileRecord = ParseFileRecord(recordNode);
                                if (fileRecord != null && fileRecord.SourceFileName.Contains("Root.wad", StringComparison.OrdinalIgnoreCase)) {
                                    fileList.Records.Add(fileRecord);
                                    break; // Found Root.wad, no need to continue
                                }
                            }
                        }
                        continue;
                    }
                    
                    // Look for RECORD child element in other sections
                    var recordNode2 = fileGroupNode.SelectSingleNode("RECORD");
                    if (recordNode2 != null) {
                        var fileRecord = ParseFileRecord(recordNode2);
                        if (fileRecord != null) {
                            fileList.Records.Add(fileRecord);
                        }
                    }
                }
            }
            
            return fileList;
        }
        catch (Exception ex) {
            throw new InvalidOperationException($"Failed to fetch file list for revision {revision}: {ex.Message}", ex);
        }
    }
    
    private FileRecord? ParseFileRecord(XmlNode recordNode) {
        try {
            var fileRecord = new FileRecord();
            
            foreach (XmlNode childNode in recordNode.ChildNodes) {
                if (childNode.NodeType != XmlNodeType.Element) continue;
                
                switch (childNode.Name) {
                    case "SrcFileName":
                        fileRecord.SourceFileName = childNode.InnerText ?? string.Empty;
                        break;
                    case "TarFileName":
                        fileRecord.TargetFileName = childNode.InnerText ?? string.Empty;
                        break;
                    case "FileType":
                        if (int.TryParse(childNode.InnerText, out int fileType)) {
                            fileRecord.FileType = fileType;
                        }
                        break;
                    case "Size":
                        if (long.TryParse(childNode.InnerText, out long size)) {
                            fileRecord.Size = size;
                        }
                        break;
                    case "HeaderSize":
                        if (long.TryParse(childNode.InnerText, out long headerSize)) {
                            fileRecord.HeaderSize = headerSize;
                        }
                        break;
                    case "CompressedHeaderSize":
                        if (long.TryParse(childNode.InnerText, out long compressedHeaderSize)) {
                            fileRecord.CompressedHeaderSize = compressedHeaderSize;
                        }
                        break;
                    case "CRC":
                        fileRecord.CRC = childNode.InnerText ?? string.Empty;
                        break;
                    case "HeaderCRC":
                        fileRecord.HeaderCRC = childNode.InnerText ?? string.Empty;
                        break;
                }
            }
            
            // Only return valid records with a source filename
            return !string.IsNullOrEmpty(fileRecord.SourceFileName) ? fileRecord : null;
        }
        catch {
            return null;
        }
    }

    public async Task<string> DownloadFileAsync(FileRecord fileRecord, string revision, IProgress<DownloadProgress>? progress = null) {
        var fileName = Path.GetFileName(fileRecord.SourceFileName);
        var localFilePath = ResolveInside(_cacheDirectory, Path.Combine(revision, fileName));
        
        var progressInfo = new DownloadProgress {
            FileName = fileName,
            TotalBytes = fileRecord.Size,
            Status = "Starting"
        };
        
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(localFilePath)!);
            
            if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length == fileRecord.Size) {
                progressInfo.Status = "Cached";
                progressInfo.BytesDownloaded = fileRecord.Size;
                progress?.Report(progressInfo);
                return localFilePath;
            }

            var baseUrl = GetRevisionsUrl().Replace("/revisions", "");
            var downloadUrl = $"{baseUrl}/{revision}/{fileRecord.SourceFileName}";
            
            progressInfo.Status = "Downloading";
            progress?.Report(progressInfo);
            
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            
            var buffer = new byte[8192];
            int bytesRead;
            long totalBytesRead = 0;
            
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                
                progressInfo.BytesDownloaded = totalBytesRead;
                progressInfo.Status = "Downloading";
                progress?.Report(progressInfo);
            }
            
            progressInfo.Status = "Completed";
            progress?.Report(progressInfo);
            
            return localFilePath;
        }
        catch (Exception ex) {
            progressInfo.Status = $"Failed: {ex.Message}";
            progress?.Report(progressInfo);
            throw new InvalidOperationException($"Failed to download file {fileName}: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> DownloadFilesAsync(IEnumerable<FileRecord> files, string revision, IProgress<DownloadProgress>? progress = null) {
        var downloadedFiles = new List<string>();
        
        foreach (var file in files) {
            try {
                var localPath = await DownloadFileAsync(file, revision, progress);
                downloadedFiles.Add(localPath);
            }
            catch (Exception ex) {
                var progressInfo = new DownloadProgress {
                    FileName = Path.GetFileName(file.SourceFileName),
                    Status = $"Failed: {ex.Message}"
                };
                progress?.Report(progressInfo);
            }
        }
        
        return downloadedFiles;
    }

    public bool IsFileCached(FileRecord fileRecord, string revision) {
        var fileName = Path.GetFileName(fileRecord.SourceFileName);
        var localFilePath = Path.Combine(_cacheDirectory, revision, fileName);
        
        if (!File.Exists(localFilePath)) return false;
        
        var fileInfo = new FileInfo(localFilePath);
        return fileInfo.Length == fileRecord.Size;
    }

    public string GetCacheDirectory() {
        return _cacheDirectory;
    }

    public long GetCacheSize() {
        if (!Directory.Exists(_cacheDirectory)) return 0;
        
        var directoryInfo = new DirectoryInfo(_cacheDirectory);
        return directoryInfo.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
    }

    public void ClearCache() {
        if (Directory.Exists(_cacheDirectory)) {
            Directory.Delete(_cacheDirectory, true);
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public bool IsRootWadCached() {
        var selectedRevision = GetSelectedRevision();
        if (string.IsNullOrEmpty(selectedRevision)) return false;
        
        var rootWadPath = Path.Combine(_cacheDirectory, selectedRevision, "Root.wad");
        return File.Exists(rootWadPath);
    }

    public void Dispose() {
        _httpClient?.Dispose();
    }

}