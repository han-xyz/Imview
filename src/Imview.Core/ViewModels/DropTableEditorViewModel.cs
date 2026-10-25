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
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Imview.Core.Database.Models;
using Imview.Core.Services;
using Imview.Core.Controls.Requirements;
using Imcodec.ObjectProperty.TypeCache;

namespace Imview.Core.ViewModels;

public class DropTableEditorViewModel : ViewModelBase {

    private readonly MainWindowViewModel _mainViewModel;
    private readonly DropTableService _dropTableService;
    private DropTable? _selectedDropTable;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    public ObservableCollection<DropTable> DropTables { get; } = new();
    public ObservableCollection<DropItemViewModel> Items { get; } = new();

    public DropTable? SelectedDropTable {
        get => _selectedDropTable;
        set {
            this.RaiseAndSetIfChanged(ref _selectedDropTable, value);
            LoadSelectedTable();
        }
    }

    public string SearchText {
        get => _searchText;
        set {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            _ = SearchDropTablesAsync();
        }
    }

    public bool IsLoading {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string StatusMessage {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public double RollChance {
        get => _selectedDropTable?.RollChance ?? 1.0;
        set {
            if (_selectedDropTable != null) {
                _selectedDropTable.RollChance = Math.Max(0, Math.Min(1, value));
                this.RaisePropertyChanged();
            }
        }
    }

    public int Weight {
        get => _selectedDropTable?.Weight ?? 100;
        set {
            if (_selectedDropTable != null) {
                _selectedDropTable.Weight = Math.Max(0, value);
                this.RaisePropertyChanged();
            }
        }
    }

    public double NoneChance {
        get => _selectedDropTable?.NoneChance ?? 0.0;
        set {
            if (_selectedDropTable != null) {
                _selectedDropTable.NoneChance = Math.Max(0, Math.Min(1, value));
                this.RaisePropertyChanged();
            }
        }
    }

    public double PityCounter {
        get => _selectedDropTable?.PityCounter ?? 0.0;
        set {
            if (_selectedDropTable != null) {
                _selectedDropTable.PityCounter = Math.Max(0, value);
                this.RaisePropertyChanged();
            }
        }
    }

    public int MinGold {
        get => _selectedDropTable?.MinGold ?? 0;
        set {
            if (_selectedDropTable != null) {
                _selectedDropTable.MinGold = Math.Max(0, value);
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(RewardSummary));
            }
        }
    }

    public int MaxGold {
        get => _selectedDropTable?.MaxGold ?? 0;
        set {
            if (_selectedDropTable != null) {
                _selectedDropTable.MaxGold = Math.Max(0, value);
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(RewardSummary));
            }
        }
    }

    public int ExperienceAmount {
        get => _selectedDropTable?.ExperienceAmount ?? 0;
        set {
            if (_selectedDropTable != null) {
                _selectedDropTable.ExperienceAmount = Math.Max(0, value);
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(RewardSummary));
            }
        }
    }

    public int TrainingPoints {
        get => _selectedDropTable?.TrainingPoints ?? 0;
        set {
            if (_selectedDropTable != null) {
                _selectedDropTable.TrainingPoints = Math.Max(0, value);
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(RewardSummary));
            }
        }
    }

    public bool GrantsPotionSlot {
        get => _selectedDropTable?.GrantsPotionSlot ?? false;
        set {
            if (_selectedDropTable != null) {
                _selectedDropTable.GrantsPotionSlot = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(RewardSummary));
            }
        }
    }

    public string RewardSummary {
        get {
            if (_selectedDropTable == null) return string.Empty;
            
            var parts = new List<string>();
            
            if (_selectedDropTable.MinGold > 0 || _selectedDropTable.MaxGold > 0) {
                if (_selectedDropTable.MinGold == _selectedDropTable.MaxGold) {
                    parts.Add($"{_selectedDropTable.MinGold} gold");
                } else {
                    parts.Add($"{_selectedDropTable.MinGold}-{_selectedDropTable.MaxGold} gold");
                }
            }
            
            if (_selectedDropTable.ExperienceAmount > 0) {
                parts.Add($"{_selectedDropTable.ExperienceAmount} XP");
            }
            
            if (_selectedDropTable.TrainingPoints > 0) {
                parts.Add($"{_selectedDropTable.TrainingPoints} training points");
            }

            if (_selectedDropTable.GrantsPotionSlot) {
                parts.Add("1 potion slot");
            }

            if (_selectedDropTable.Items.Count > 0) {
                parts.Add($"{_selectedDropTable.Items.Count} items");
            }

            return parts.Count > 0
                ? $"This table gives: {string.Join(", ", parts)}"
                : "This table gives no rewards";
        }
    }

    public ICommand CreateTableCommand { get; }
    public ICommand DeleteTableCommand { get; }
    public ICommand SaveTableCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand EditItemRequirementsCommand { get; }

    public DropTableEditorViewModel(MainWindowViewModel mainViewModel) {
        _mainViewModel = mainViewModel;
        _dropTableService = new DropTableService();

        CreateTableCommand = ReactiveCommand.CreateFromTask(ShowCreateTableDialogAsync);
        DeleteTableCommand = ReactiveCommand.CreateFromTask(DeleteTableAsync,
            this.WhenAnyValue(x => x.SelectedDropTable).Select(x => x != null));
        SaveTableCommand = ReactiveCommand.CreateFromTask(SaveTableAsync,
            this.WhenAnyValue(x => x.SelectedDropTable).Select(x => x != null));
        AddItemCommand = ReactiveCommand.Create(AddItem,
            this.WhenAnyValue(x => x.SelectedDropTable).Select(x => x != null));
        RemoveItemCommand = ReactiveCommand.Create<DropItemViewModel>(RemoveItem);
        RefreshCommand = ReactiveCommand.CreateFromTask(LoadDropTablesAsync);
        EditItemRequirementsCommand = ReactiveCommand.CreateFromTask<DropItemViewModel>(EditItemRequirementsAsync,
            this.WhenAnyValue(x => x.SelectedDropTable).Select(x => x != null));

        _ = LoadDropTablesAsync();
    }

    private async Task ShowCreateTableDialogAsync() {
        try {
            var dialog = new Views.CreateDropTableDialog();
            var mainWindow = _mainViewModel.GetMainWindow();
            
            if (mainWindow != null) {
                await dialog.ShowDialog(mainWindow);
                
                if (dialog.WasCreated) {
                    IsLoading = true;
                    StatusMessage = "Creating drop table...";

                    var id = await _dropTableService.CreateDropTableAsync(dialog.TableName, dialog.TableDescription);
                    if (id != null) {
                        StatusMessage = $"Drop table '{dialog.TableName}' created successfully.";
                        await LoadDropTablesAsync();
                        
                        var newTable = DropTables.FirstOrDefault(t => t.Name == id);
                        if (newTable != null) {
                            SelectedDropTable = newTable;
                        }
                    } else {
                        StatusMessage = "Failed to create drop table.";
                    }
                }
            }
        }
        catch (Exception ex) {
            StatusMessage = $"Error creating drop table: {ex.Message}";
        }
        finally {
            IsLoading = false;
        }
    }

    private async Task LoadDropTablesAsync() {
        try {
            IsLoading = true;
            StatusMessage = "Loading drop tables...";

            var tables = await _dropTableService.GetAllDropTablesAsync();
            
            DropTables.Clear();
            foreach (var table in tables) {
                DropTables.Add(table);
            }

            StatusMessage = $"Loaded {tables.Count} drop tables.";
        }
        catch (Exception ex) {
            StatusMessage = $"Error loading drop tables: {ex.Message}";
        }
        finally {
            IsLoading = false;
        }
    }

    private async Task SearchDropTablesAsync() {
        try {
            IsLoading = true;

            var tables = string.IsNullOrWhiteSpace(SearchText) 
                ? await _dropTableService.GetAllDropTablesAsync()
                : await _dropTableService.SearchDropTablesAsync(SearchText);
            
            DropTables.Clear();
            foreach (var table in tables) {
                DropTables.Add(table);
            }
        }
        catch (Exception ex) {
            StatusMessage = $"Error searching drop tables: {ex.Message}";
        }
        finally {
            IsLoading = false;
        }
    }


    private async Task DeleteTableAsync() {
        if (SelectedDropTable == null) return;

        try {
            IsLoading = true;
            StatusMessage = "Deleting drop table...";

            var success = await _dropTableService.DeleteDropTableAsync(SelectedDropTable.Name);
            if (success) {
                StatusMessage = $"Drop table '{SelectedDropTable.Name}' deleted successfully.";
                await LoadDropTablesAsync();
                SelectedDropTable = null;
            } else {
                StatusMessage = "Failed to delete drop table.";
            }
        }
        catch (Exception ex) {
            StatusMessage = $"Error deleting drop table: {ex.Message}";
        }
        finally {
            IsLoading = false;
        }
    }

    private async Task SaveTableAsync() {
        if (SelectedDropTable == null) return;

        try {
            IsLoading = true;
            StatusMessage = "Saving drop table...";

            UpdateTableFromItems();

            if (!_dropTableService.ValidateDropTable(SelectedDropTable, out var errors)) {
                StatusMessage = $"Validation failed: {string.Join(", ", errors)}";
                return;
            }

            var success = await _dropTableService.UpdateDropTableAsync(SelectedDropTable);
            if (success) {
                StatusMessage = $"Drop table '{SelectedDropTable.Name}' saved successfully.";
            } else {
                StatusMessage = "Failed to save drop table.";
            }
        }
        catch (Exception ex) {
            StatusMessage = $"Error saving drop table: {ex.Message}";
        }
        finally {
            IsLoading = false;
        }
    }

    private void AddItem() {
        if (SelectedDropTable == null) return;

        var newItem = new DropItem {
            ItemId = "",
            ItemName = "New Item",
            Notes = ""
        };

        var viewModel = new DropItemViewModel(newItem);
        Items.Add(viewModel);
    }

    private void RemoveItem(DropItemViewModel item) {
        if (item != null) {
            Items.Remove(item);
        }
    }

    private void LoadSelectedTable() {
        Items.Clear();
        
        if (SelectedDropTable?.Items != null) {
            foreach (var item in SelectedDropTable.Items) {
                Items.Add(new DropItemViewModel(item));
            }
        }

        this.RaisePropertyChanged(nameof(RollChance));
        this.RaisePropertyChanged(nameof(Weight));
        this.RaisePropertyChanged(nameof(NoneChance));
        this.RaisePropertyChanged(nameof(PityCounter));
        this.RaisePropertyChanged(nameof(MinGold));
        this.RaisePropertyChanged(nameof(MaxGold));
        this.RaisePropertyChanged(nameof(ExperienceAmount));
        this.RaisePropertyChanged(nameof(TrainingPoints));
        this.RaisePropertyChanged(nameof(GrantsPotionSlot));
        this.RaisePropertyChanged(nameof(RewardSummary));
    }

    private void UpdateTableFromItems() {
        if (SelectedDropTable == null) return;

        SelectedDropTable.Items.Clear();
        foreach (var itemVm in Items) {
            SelectedDropTable.Items.Add(itemVm.Model);
        }
    }

    private async Task EditItemRequirementsAsync(DropItemViewModel item) {
        try {
            var factory = new RequirementEditorFactory();
            var editor = new RequirementListEditor(item.Requirements, factory);
            var mainWindow = _mainViewModel.GetMainWindow();
            
            if (mainWindow != null) {
                await editor.ShowDialog(mainWindow);
                var result = await editor.GetResultAsync();
                if (result != null) {
                    item.Requirements = result;
                }
            }
        }
        catch (Exception ex) {
            StatusMessage = $"Error editing requirements: {ex.Message}";
        }
    }
}

public class DropItemViewModel : ViewModelBase {
    public DropItem Model { get; }

    public string ItemId {
        get => Model.ItemId;
        set {
            Model.ItemId = value;
            this.RaisePropertyChanged();
        }
    }

    public string ItemName {
        get => Model.ItemName;
        set {
            Model.ItemName = value;
            this.RaisePropertyChanged();
        }
    }

    public string Notes {
        get => Model.Notes;
        set {
            Model.Notes = value;
            this.RaisePropertyChanged();
        }
    }

    public RequirementList? Requirements {
        get => Model.Requirements;
        set {
            Model.Requirements = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(HasRequirements));
            this.RaisePropertyChanged(nameof(RequirementsText));
        }
    }

    public bool HasRequirements => Requirements?.m_requirements?.Count > 0;

    public string RequirementsText => HasRequirements 
        ? $"{Requirements!.m_requirements!.Count} requirement(s)" 
        : "No requirements";

    public DropItemViewModel(DropItem model) {
        Model = model;
    }
}