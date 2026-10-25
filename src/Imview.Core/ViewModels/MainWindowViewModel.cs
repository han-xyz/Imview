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
using System.Windows.Input;
using Imview.Core.ViewModels;
using ReactiveUI;
using Avalonia.Notification;
using Imview.Core.Services;
using Imview.Core.Data;
using Avalonia.Controls;
using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using System.Threading.Tasks;
using Imview.Core.Views;

namespace Imview.Core.ViewModels;

public class MainWindowViewModel : ViewModelBase {

    public INotificationMessageManager Manager { get; } = new NotificationMessageManager();
    public ICommand CreateQuestCommand { get; }
    public ICommand LoadQuestCommand { get; }

    private Avalonia.Controls.Window? _mainWindow;
    private QuestBrowserViewModel? _questBrowserViewModel;

    public MainWindowViewModel() {
        TabManager = new TabManagerViewModel();
        TabManager.Initialize(this);
        
        CreateQuestCommand = ReactiveCommand.Create(CreateNewQuest);
        LoadQuestCommand = ReactiveCommand.Create(LoadQuest);

        MessageService.Initialize(Manager);
    }

    public void Initialize(Avalonia.Controls.Window window) {
        _mainWindow = window;
        
        // Try to load Root.wad from cache on startup
        _ = Task.Run(LoadRootWadAsync);
    }
    
    private async Task LoadRootWadAsync() {
        try {
            var loaded = await RootWadService.Instance.LoadFromCacheAsync();
            if (loaded) {
                var info = RootWadService.Instance.GetInfo();
                MessageService.Info($"Root.wad loaded: {info.FileCount} files ({info.SizeFormatted})")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
                
                // Wait a moment for locale loading to complete, then show status
                await Task.Delay(2000);
                var localeInfo = LocaleService.Instance.GetInfo();
                if (localeInfo.IsLoaded) {
                    MessageService.Info($"Locale data loaded: {localeInfo.CategoryCount} categories, {localeInfo.TotalStringCount} strings")
                        .WithDuration(TimeSpan.FromSeconds(3))
                        .Send();
                }
            }
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to load Root.wad: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    public TabManagerViewModel TabManager { get; }

    public void CreateNewQuest() {
        var tab = TabManager.AddTab("New Quest", new QuestTemplateEditorViewModel(this));
        TabManager.SelectTab(tab);
    }

    public async void LoadQuest() {
        try {
            if (_mainWindow == null) {
                MessageService.Error("Main window is not initialized.")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();

                return;
            }

            var template = await TemplateSerializer.LoadTemplateAsync(_mainWindow);
            if (template != null) {
                var tab = TabManager.AddTab("Loaded Quest", new QuestTemplateEditorViewModel(this, template));
                TabManager.SelectTab(tab);
                MessageService.Info("Quest template loaded successfully!")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
            }
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to load quest template: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    public async void GetQuestsFromPacketCapture() {
        try {
            if (_mainWindow == null) {
                MessageService.Error("Main window is not initialized.")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();

                return;
            }

            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions {
                Title = "Select Packet Capture File",
                AllowMultiple = false,
                FileTypeFilter = [
                new("JSON Packet Capture") {
                    Patterns = ["*.json"]
                },
                new("All Files") {
                    Patterns = ["*.*"]
                }
            ]
            };

            var files = await _mainWindow.StorageProvider.OpenFilePickerAsync(options);
            if (files == null || files.Count == 0) {
                // User cancelled.
                return;
            }

            var filePath = files[0].Path.LocalPath;

            MessageService.Info("Reading packet capture, please wait...")
                .WithDuration(TimeSpan.FromSeconds(3))
                .Send();

            var questTemplates = await QuestPacketReaderService.ReadQuestsFromPacketCaptureAsync(filePath);

            // Switch to the packet view with the extracted quest templates.
            var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var tab = TabManager.AddTab($"Packets: {fileName}", new PacketQuestViewModel(this, filePath, questTemplates));
            TabManager.SelectTab(tab);
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to load packet capture: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    /// <summary>
    /// Opens a quest template editor in a new tab.
    /// </summary>
    /// <param name="template">The quest template to edit</param>
    public void OpenQuestEditorInNewTab(QuestTemplate template) {
        try {
            // Create the editor in read-only mode in a new tab
            var tab = TabManager.AddTab("Quest Editor (Read-Only)", new QuestTemplateEditorViewModel(this, template));
            TabManager.SelectTab(tab);
        }
        catch (Exception ex) {
            MessageService.Error($"Error opening quest editor: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    /// <summary>
    /// Unpacks a KIWAD file, with optional deserialization.
    /// </summary>
    /// <param name="deserialize">Whether to attempt to deserialize files with supported extensions</param>
    public async void UnpackKiwad(bool deserialize) {
        try {
            if (_mainWindow == null) {
                MessageService.Error("Main window is not initialized.")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();
                    
                return;
            }

            var result = await KIWadFileService.UnpackKiwadArchiveAsync(_mainWindow, deserialize);
            if (result) {
                MessageService.Info($"KIWAD archive unpacked successfully{(deserialize ? " with deserialization" : "")}.")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
            }
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to unpack KIWAD: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    public void AnalyzeObjectPropertyBlob() {
        var tab = TabManager.AddTab("Analyze Blob", new ObjectPropertyBlobViewModel(this));
        TabManager.SelectTab(tab);
    }

    /// <summary>
    /// Returns the main window instance.
    /// </summary>
    public Avalonia.Controls.Window? GetMainWindow()
        => _mainWindow;

    public void ReturnToSplash() {
        var homeTab = TabManager.FindTabByContent<SplashPageViewModel>();
        if (homeTab != null) {
            TabManager.SelectTab(homeTab);
        }
    }

    /// <summary>
    /// Opens the quest browser in a new tab.
    /// </summary>
    public void ShowQuestBrowser() {
        // Check if quest browser tab already exists
        var existingTab = TabManager.FindTabByContent<QuestBrowserViewModel>();
        if (existingTab != null) {
            TabManager.SelectTab(existingTab);
            return;
        }

        // Create singleton quest browser instance if it doesn't exist
        if (_questBrowserViewModel == null) {
            _questBrowserViewModel = new QuestBrowserViewModel();
        }

        var tab = TabManager.AddTab("Quest Editor", _questBrowserViewModel);
        TabManager.SelectTab(tab);
    }

    /// <summary>
    /// Shows the database configuration dialog.
    /// </summary>
    public async void ShowDatabaseConfig() {
        try {
            if (_mainWindow == null) {
                MessageService.Error("Main window is not initialized.")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();
                return;
            }

            var configDialog = new DatabaseConfigWindow();
            var result = await configDialog.ShowDialog<bool?>(_mainWindow);
            
            if (result == true) {
                MessageService.Info("Database configuration updated successfully!")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
            }
        }
        catch (Exception ex) {
            MessageService.Error($"Error showing database configuration: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    /// <summary>
    /// Shows the client file configuration dialog.
    /// </summary>
    public async void ShowClientFileConfig() {
        try {
            if (_mainWindow == null) {
                MessageService.Error("Main window is not initialized.")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();
                return;
            }

            var configDialog = new ClientFileConfigWindow();
            var result = await configDialog.ShowDialog<bool?>(_mainWindow);
            
            if (result == true) {
                MessageService.Info("Client file configuration updated successfully!")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
            }
        }
        catch (Exception ex) {
            MessageService.Error($"Error showing client file configuration: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    /// <summary>
    /// Shows both database and client file configuration dialogs.
    /// </summary>
    public async void ShowDatabaseAndClientConfig() {
        ShowDatabaseConfig();
        await Task.Delay(500); // Small delay between dialogs
        ShowClientFileConfig();
    }

    /// <summary>
    /// Opens the Zone Editor in a new tab.
    /// </summary>
    public void ShowZoneEditor() {
        var tab = TabManager.AddTab("Zone Editor", new ZoneEditorViewModel(this));
        TabManager.SelectTab(tab);
    }

    /// <summary>
    /// Opens the WAD Viewer in a new tab.
    /// </summary>
    public void ShowWadViewer() {
        var tab = TabManager.AddTab("WAD Viewer", new WadViewerViewModel(this));
        TabManager.SelectTab(tab);
    }

    /// <summary>
    /// Shows the client file download dialog.
    /// </summary>
    public void ShowClientFileDownload() {
        try {
            if (_mainWindow == null) {
                MessageService.Error("Main window is not initialized.")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();
                return;
            }

            var clientFileService = new Services.ClientFileService();
            var selectedRevision = clientFileService.GetSelectedRevision();
            
            if (string.IsNullOrEmpty(selectedRevision)) {
                MessageService.Info("Please configure and select a client revision first.")
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();
                return;
            }

            var downloadWindow = new ClientFileDownloadWindow();
            downloadWindow.Show(_mainWindow);
        }
        catch (Exception ex) {
            MessageService.Error($"Error showing client file download: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    /// <summary>
    /// Opens the Drop Table Editor in a new tab.
    /// </summary>
    public async void ShowDropTableEditor() {
        var isConfigured = await DataPack.IsLoadedAsync();
        
        if (!isConfigured) {
            MessageService.Info("Data pack configuration required to edit drop tables.")
                .WithDuration(TimeSpan.FromSeconds(3))
                .Send();
            return;
        }

        // Check if drop table editor tab already exists
        var existingTab = TabManager.FindTabByContent<DropTableEditorViewModel>();
        if (existingTab != null) {
            TabManager.SelectTab(existingTab);
            return;
        }

        var tab = TabManager.AddTab("Drop Table Editor", new DropTableEditorViewModel(this));
        TabManager.SelectTab(tab);
    }

}