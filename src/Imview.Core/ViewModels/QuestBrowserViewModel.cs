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
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Imview.Core.Database.Collections;
using Imview.Core.Services;
using Imview.Core.Views;
using System.Text.Json;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Controls.Templates;
using Imview.Core.Common.Extensions;

namespace Imview.Core.ViewModels;

public class QuestBrowserViewModel : ViewModelBase {
    
    private readonly ObservableCollection<QuestDocument> _quests = new();
    private readonly ObservableCollection<QuestDocument> _filteredQuests = new();
    private QuestDocument? _selectedQuest;
    private string _searchText = "";
    private bool _isLoading = false;
    private string _statusText = "";
    private bool _isEditing = false;
    private QuestTemplateEditorViewModel? _questEditorViewModel;
    private bool _hasUnsavedChanges = false;
    private QuestDocument? _originalQuest;

    public QuestBrowserViewModel() {
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        ImportPacketCaptureCommand = ReactiveCommand.CreateFromTask(ImportPacketCaptureAsync);
        NewQuestCommand = ReactiveCommand.Create(NewQuest);
        LoadLocalQuestCommand = ReactiveCommand.CreateFromTask(LoadLocalQuestAsync);
        DeleteSelectedCommand = ReactiveCommand.CreateFromTask(DeleteSelectedAsync, 
            this.WhenAnyValue(x => x.HasSelectedQuest));
        EditCommand = ReactiveCommand.Create(ToggleEdit);
        OpenEditorCommand = ReactiveCommand.Create(OpenEditor, 
            this.WhenAnyValue(x => x.HasSelectedQuest));
        ExportCommand = ReactiveCommand.CreateFromTask(ExportAsync, 
            this.WhenAnyValue(x => x.HasSelectedQuest));
        SaveQuestCommand = ReactiveCommand.CreateFromTask(SaveQuestAsync, 
            this.WhenAnyValue(x => x.HasSelectedQuest));

        // Set up search filtering
        this.WhenAnyValue(x => x.SearchText)
            .Subscribe(_ => FilterQuests());

        // Set up property change notifications for quest selection
        this.WhenAnyValue(x => x.SelectedQuest)
            .Subscribe(async newQuest => {
                this.RaisePropertyChanged(nameof(HasSelectedQuest));
                this.RaisePropertyChanged(nameof(SelectedQuestPreview));
                await HandleQuestSelectionChangeInternal(newQuest);
            });

        // Load quests on initialization
        _ = RefreshAsync();

        // Subscribe to packet capture import events
        DialogPacketImportService.Instance.PacketCaptureImported += (s, e) => {
            this.RaisePropertyChanged(nameof(HasImportedPacketCapture));
        };
    }

    public ObservableCollection<QuestDocument> FilteredQuests => _filteredQuests;

    public QuestDocument? SelectedQuest {
        get => _selectedQuest;
        set => this.RaiseAndSetIfChanged(ref _selectedQuest, value);
    }

    public string SearchText {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public bool IsLoading {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string StatusText {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public bool IsEditing {
        get => _isEditing;
        set => this.RaiseAndSetIfChanged(ref _isEditing, value);
    }

    public bool HasSelectedQuest => SelectedQuest != null;

    public bool HasImportedPacketCapture => DialogPacketImportService.Instance.HasImportedPacketCapture;

    public QuestTemplateEditorViewModel? QuestEditorViewModel {
        get => _questEditorViewModel;
        private set => this.RaiseAndSetIfChanged(ref _questEditorViewModel, value);
    }

    public bool HasUnsavedChanges {
        get => _hasUnsavedChanges;
        private set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value);
    }

    public string QuestCountText => $"Quests ({_filteredQuests.Count})";

    public string EditButtonText => IsEditing ? "Save Changes" : "Edit";

    public string SelectedQuestPreview {
        get {
            if (SelectedQuest?.Template == null) return "";
            try {
                return JsonSerializer.Serialize(SelectedQuest.Template, new JsonSerializerOptions { 
                    WriteIndented = true 
                });
            }
            catch {
                return "Unable to preview template";
            }
        }
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportPacketCaptureCommand { get; }
    public ReactiveCommand<Unit, Unit> NewQuestCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadLocalQuestCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteSelectedCommand { get; }
    public ReactiveCommand<Unit, Unit> EditCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenEditorCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveQuestCommand { get; }

    private async Task RefreshAsync() {
        try {
            IsLoading = true;
            StatusText = "Loading quests...";

            // Check if database is configured
            if (!DatabaseConfigService.IsDatabaseConfigured()) {
                StatusText = "Database not configured - configure in Settings to browse quests";
                return;
            }

            var quests = await QuestCollection.GetAllQuestsAsync();
            
            _quests.Clear();
            foreach (var quest in quests) {
                _quests.Add(quest);
            }

            FilterQuests();
            StatusText = quests.Count == 0 ? "No quests found in database" : $"Loaded {quests.Count} quest(s)";
        }
        catch (Exception ex) {
            StatusText = $"Error loading quests: {ex.Message}";
        }
        finally {
            IsLoading = false;
        }
    }

    private async Task ImportPacketCaptureAsync() {
        try {
            // Get reference to main window for file dialog
            var app = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;

            if (mainWindow == null) {
                StatusText = "Could not access main window for file dialog";
                return;
            }

            // Open file dialog to select packet capture
            var dialog = new OpenFileDialog {
                Title = "Select Packet Capture File",
                Filters = new List<FileDialogFilter> {
                    new() { Name = "JSON Files", Extensions = { "json" } },
                    new() { Name = "All Files", Extensions = { "*" } }
                }
            };

            var result = await dialog.ShowAsync(mainWindow);
            if (result == null || result.Length == 0) {
                StatusText = "No file selected";
                return;
            }

            var filePath = result[0];
            StatusText = $"Importing packet capture: {filePath}";

            // Import using the service
            var success = await DialogPacketImportService.Instance.ImportPacketCaptureAsync(filePath);
            if (success) {
                StatusText = $"Successfully imported {DialogPacketImportService.Instance.CapturedDialogs.Count} dialog entries";

                // Raise property changed for HasImportedPacketCapture
                this.RaisePropertyChanged(nameof(HasImportedPacketCapture));

                // Open the import window to view dialogs
                var importWindow = new DialogPacketImportWindow();
                await importWindow.ShowDialog(mainWindow);
            } else {
                StatusText = "Failed to import packet capture";
            }
        }
        catch (Exception ex) {
            StatusText = $"Error importing packet capture: {ex.Message}";
        }
    }

    private void FilterQuests() {
        _filteredQuests.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchText) 
            ? _quests
            : _quests.Where(q => 
                (q.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (q.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

        foreach (var quest in filtered) {
            _filteredQuests.Add(quest);
        }

        this.RaisePropertyChanged(nameof(QuestCountText));
    }

    private void NewQuest() {
        try {
            // Create a new empty quest template for editing
            var newTemplate = new QuestTemplate();
            SelectedQuest = new QuestDocument {
                Id = "new",
                Name = "New Quest",
                Description = "A new quest template",
                Template = newTemplate,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = Environment.UserName,
                ModifiedBy = Environment.UserName
            };
            StatusText = "Created new quest template";
        }
        catch (Exception ex) {
            StatusText = $"Error creating new quest: {ex.Message}";
        }
    }

    private async Task LoadLocalQuestAsync() {
        try {
            // Get reference to main window for file dialog
            var app = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;
            
            if (mainWindow == null) {
                StatusText = "Could not access main window for file dialog";
                return;
            }

            var template = await TemplateSerializer.LoadTemplateAsync(mainWindow);
            if (template != null) {
                // Create a quest document for the loaded template
                SelectedQuest = new QuestDocument {
                    Id = "local",
                    Name = template.m_questName.OrIfEmpty("Loaded Quest"),
                    Description = "Loaded from local file",
                    Template = template,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    CreatedBy = Environment.UserName,
                    ModifiedBy = Environment.UserName
                };
                StatusText = "Loaded quest from local file";
            } else {
                StatusText = "No quest file selected or load cancelled";
            }
        }
        catch (Exception ex) {
            StatusText = $"Error loading local quest: {ex.Message}";
        }
    }

    private async Task DeleteSelectedAsync() {
        if (SelectedQuest == null) return;

        try {
            // Show confirmation dialog
            var confirmed = await ShowDeleteConfirmationDialog(SelectedQuest.Name);
            if (!confirmed) {
                return; // User cancelled
            }

            var success = await QuestCollection.DeleteQuestAsync(SelectedQuest.Id);
            if (success) {
                StatusText = $"Deleted quest '{SelectedQuest.Name}'";
                await RefreshAsync();
                SelectedQuest = null;
            } else {
                StatusText = "Failed to delete quest";
            }
        }
        catch (Exception ex) {
            StatusText = $"Error deleting quest: {ex.Message}";
        }
    }

    private async void ToggleEdit() {
        if (!IsEditing) {
            IsEditing = true;
        } else {
            // Save changes
            if (SelectedQuest != null) {
                try {
                    var success = await QuestCollection.UpdateQuestAsync(
                        SelectedQuest.Id, 
                        SelectedQuest.Template, 
                        SelectedQuest.Name, 
                        SelectedQuest.Description);
                    
                    if (success) {
                        StatusText = "Quest updated successfully";
                        await RefreshAsync();
                    } else {
                        StatusText = "Failed to update quest";
                    }
                } catch (Exception ex) {
                    StatusText = $"Error updating quest: {ex.Message}";
                }
            }
            IsEditing = false;
        }
    }

    private void OpenEditor() {
        if (SelectedQuest?.Template == null) return;
        
        try {
            // Get reference to main window through application
            var app = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;
            
            if (mainWindow?.DataContext is MainWindowViewModel mainViewModel) {
                // Open the quest template in the editor
                mainViewModel.OpenQuestEditorInNewTab(SelectedQuest.Template);
                StatusText = $"Opened '{SelectedQuest.Name}' in quest editor";
            } else {
                StatusText = "Could not access main window to open quest editor";
            }
        }
        catch (Exception ex) {
            StatusText = $"Error opening quest editor: {ex.Message}";
        }
    }

    private async Task ExportAsync() {
        if (SelectedQuest?.Template == null) return;

        try {
            // Get the main window for the file dialog
            var app = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var parentWindow = app?.MainWindow;
            
            if (parentWindow != null) {
                var success = await TemplateSerializer.SaveTemplateToFileAsync(SelectedQuest.Template, parentWindow);
                if (success) {
                    StatusText = $"Exported '{SelectedQuest.Name}' to file";
                } else {
                    StatusText = "Export cancelled or failed";
                }
            }
        }
        catch (Exception ex) {
            StatusText = $"Error exporting quest: {ex.Message}";
        }
    }

    private void UpdateQuestEditor() {
        if (SelectedQuest?.Template != null) {
            // Sync template name to quest name if template has a name
            var templateName = SelectedQuest.Template.m_questName.ToString();
            if (!string.IsNullOrEmpty(templateName) && SelectedQuest.Name != templateName) {
                SelectedQuest.Name = templateName;
            }

            // Only create a new editor if we don't have one or it's for a different quest
            if (QuestEditorViewModel == null || QuestEditorViewModel.Template != SelectedQuest.Template) {
                QuestEditorViewModel = new QuestTemplateEditorViewModel(null!, SelectedQuest.Template);
            }
        } else {
            QuestEditorViewModel = null;
        }
    }

    private async Task SaveQuestAsync() {
        if (SelectedQuest == null) return;

        try {
            // Show save options dialog
            var saveLocation = await ShowSaveOptionsDialog();
            
            if (saveLocation == SaveLocation.Cancelled) {
                return;
            }

            if (saveLocation == SaveLocation.Database) {
                // First, ensure any editor changes are saved to the template
                SaveEditorChangesToTemplate();
                
                // Save to database
                var upsertedId = await QuestCollection.UpsertQuestAsync(
                    SelectedQuest.Template,
                    SelectedQuest.Name,
                    SelectedQuest.Description);

                if (!string.IsNullOrEmpty(upsertedId)) {
                    SelectedQuest.Id = upsertedId;
                    SelectedQuest.ModifiedAt = DateTime.UtcNow;
                    SelectedQuest.ModifiedBy = Environment.UserName;
                    
                    // Reset unsaved changes tracking
                    HasUnsavedChanges = false;
                    _originalQuest = SelectedQuest != null ? CloneQuest(SelectedQuest) : null;
                    
                    StatusText = "Quest saved to database successfully";
                    // Don't refresh - keep the current quest open for editing
                } else {
                    StatusText = "Failed to save quest to database";
                }
            } else {
                // First, ensure any editor changes are saved to the template
                SaveEditorChangesToTemplate();
                
                // Save to local file
                var app = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var mainWindow = app?.MainWindow;
                
                if (mainWindow != null) {
                    var success = await TemplateSerializer.SaveTemplateToFileAsync(SelectedQuest.Template, mainWindow);
                    if (success) {
                        // Reset unsaved changes tracking
                        HasUnsavedChanges = false;
                        _originalQuest = SelectedQuest != null ? CloneQuest(SelectedQuest) : null;
                        
                        StatusText = "Quest saved to file successfully";
                        // Don't refresh - keep the current quest open for editing
                    } else {
                        StatusText = "Failed to save quest to file or save cancelled";
                    }
                } else {
                    StatusText = "Could not access main window for file dialog";
                }
            }
        }
        catch (Exception ex) {
            StatusText = $"Error saving quest: {ex.Message}";
        }
    }

    private async Task HandleQuestSelectionChangeInternal(QuestDocument? newQuest) {
        // For now, just update the editor - we'll add unsaved changes logic later
        UpdateQuestEditor();
        SetupChangeTracking();
    }

    private async Task HandleQuestSelectionChange(QuestDocument? newQuest) {
        // Check if there are unsaved changes
        if (HasUnsavedChanges && _selectedQuest != null) {
            var result = await ShowUnsavedChangesDialog();
            
            switch (result) {
                case UnsavedChangesResult.Save:
                    await SaveQuestAsync();
                    break;
                case UnsavedChangesResult.Cancel:
                    // Cancel the selection change
                    return;
                case UnsavedChangesResult.Discard:
                    // Continue with selection change
                    break;
            }
        }

        // Proceed with the selection change
        this.RaiseAndSetIfChanged(ref _selectedQuest, newQuest);
        this.RaisePropertyChanged(nameof(HasSelectedQuest));
        this.RaisePropertyChanged(nameof(SelectedQuestPreview));
        
        // Reset unsaved changes tracking
        HasUnsavedChanges = false;
        _originalQuest = newQuest != null ? CloneQuest(newQuest) : null;
        
        UpdateQuestEditor();
        SetupChangeTracking();
    }

    private void SetupChangeTracking() {
        if (SelectedQuest != null) {
            // Monitor changes to quest properties
            this.WhenAnyValue(x => x.SelectedQuest.Name, x => x.SelectedQuest.Description)
                .Skip(1) // Skip initial value
                .Subscribe(_ => CheckForChanges());
        }
    }

    private void CheckForChanges() {
        if (_originalQuest == null || SelectedQuest == null) {
            HasUnsavedChanges = false;
            return;
        }

        var hasChanges = _originalQuest.Name != SelectedQuest.Name ||
                        _originalQuest.Description != SelectedQuest.Description ||
                        !AreTemplatesEqual(_originalQuest.Template, SelectedQuest.Template);

        HasUnsavedChanges = hasChanges;
    }

    private bool AreTemplatesEqual(QuestTemplate template1, QuestTemplate template2) {
        try {
            var json1 = JsonSerializer.Serialize(template1);
            var json2 = JsonSerializer.Serialize(template2);
            return json1 == json2;
        }
        catch {
            return false;
        }
    }

    private QuestDocument CloneQuest(QuestDocument quest) {
        return new QuestDocument {
            Id = quest.Id,
            Name = quest.Name,
            Description = quest.Description,
            Template = quest.Template, // Note: This is a shallow copy, but should be sufficient for basic change tracking
            CreatedAt = quest.CreatedAt,
            ModifiedAt = quest.ModifiedAt,
            CreatedBy = quest.CreatedBy,
            ModifiedBy = quest.ModifiedBy
        };
    }

    private async Task<SaveLocation> ShowSaveOptionsDialog() {
        try {
            var app = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;
            
            if (mainWindow == null) {
                return SaveLocation.Database; // Default to database if can't show dialog
            }

            var dialog = new SaveOptionsDialog();
            var result = await dialog.ShowDialog<SaveLocation?>(mainWindow);
            
            return result ?? SaveLocation.Cancelled;
        }
        catch {
            return SaveLocation.Database; // Default to database on error
        }
    }

    /// <summary>
    /// Saves any changes from the quest editor UI back to the template object
    /// </summary>
    private void SaveEditorChangesToTemplate() {
        // This is a bit hacky, but we need to find the QuestTemplateEditorView in the UI tree
        // and call its SaveChangesToTemplate method to ensure UI changes are captured
        try {
            // First, sync the top-level quest name to the template
            if (SelectedQuest?.Template != null && !string.IsNullOrEmpty(SelectedQuest.Name)) {
                SelectedQuest.Template.m_questName = new Imcodec.IO.ByteString(SelectedQuest.Name);
            }

            var app = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;
            
            if (mainWindow != null) {
                // Find the QuestBrowserView
                var questBrowserView = FindQuestBrowserView(mainWindow);
                if (questBrowserView != null) {
                    // Find the QuestTemplateEditorView within it
                    var editorView = FindChildOfType<Views.QuestTemplateEditorView>(questBrowserView);
                    if (editorView != null) {
                        editorView.SaveChangesToTemplate();
                    }
                }
            }
        }
        catch (Exception ex) {
            // If we can't save editor changes, log it but don't fail the save operation
            Console.WriteLine($"Warning: Could not save editor changes to template: {ex.Message}");
        }
    }

    private Views.QuestBrowserView? FindQuestBrowserView(Avalonia.Controls.Window window) {
        return FindChildOfType<Views.QuestBrowserView>(window);
    }

    private T? FindChildOfType<T>(Avalonia.StyledElement parent) where T : class {
        if (parent is T target) return target;
        
        if (parent is Avalonia.Visual visual) {
            foreach (var child in visual.GetVisualChildren()) {
                if (child is Avalonia.StyledElement styledChild) {
                    var result = FindChildOfType<T>(styledChild);
                    if (result != null) return result;
                }
            }
        }
        return null;
    }

    private async Task<bool> ShowDeleteConfirmationDialog(string questName) {
        try {
            var app = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;
            
            if (mainWindow == null) {
                return false; // Default to not delete if can't show dialog
            }

            var dialog = new DeleteConfirmationDialog(questName);
            var result = await dialog.ShowDialog<bool?>(mainWindow);
            
            return result ?? false; // Default to not delete
        }
        catch {
            return false; // Default to not delete on error
        }
    }

    private async Task<UnsavedChangesResult> ShowUnsavedChangesDialog() {
        try {
            var app = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;
            
            if (mainWindow == null) {
                return UnsavedChangesResult.Discard;
            }

            var dialog = new UnsavedChangesDialog();
            var result = await dialog.ShowDialog<UnsavedChangesResult?>(mainWindow);
            
            return result ?? UnsavedChangesResult.Cancel;
        }
        catch {
            return UnsavedChangesResult.Discard;
        }
    }
}

public enum UnsavedChangesResult {
    Save,
    Discard,
    Cancel
}