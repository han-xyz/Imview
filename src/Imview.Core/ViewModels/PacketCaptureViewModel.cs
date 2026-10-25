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
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Imcodec.ObjectProperty.TypeCache;
using ReactiveUI;
using System.Linq;
using Imview.Core.Services;
using Imview.Core.Views;

namespace Imview.Core.ViewModels;

public class PacketQuestViewModel : ViewModelBase {

    private readonly MainWindowViewModel _mainViewModel;
    private QuestTemplate? _selectedQuest;
    private string _sourceFilePath;

    public ObservableCollection<QuestTemplate> QuestTemplates { get; }
    public ICommand BackToSplashCommand { get; }
    public ICommand EditSelectedQuestCommand { get; }
    public ICommand SaveAllQuestsCommand { get; }
    public ICommand SaveIndividualQuestCommand { get; }

    public bool HasQuests => QuestTemplates != null && QuestTemplates.Count > 0;
    public bool HasSelectedQuest => SelectedQuest != null;

    public string SourceFilePath {
        get => _sourceFilePath;
        set => this.RaiseAndSetIfChanged(ref _sourceFilePath, value);
    }

    public QuestTemplate? SelectedQuest {
        get => _selectedQuest;
        set {
            this.RaiseAndSetIfChanged(ref _selectedQuest, value);
            this.RaisePropertyChanged(nameof(HasSelectedQuest));
        }
    }

    public PacketQuestViewModel(MainWindowViewModel mainViewModel, string filePath, ObservableCollection<QuestTemplate> questTemplates) {
        _mainViewModel = mainViewModel;
        _sourceFilePath = filePath;
        QuestTemplates = questTemplates ?? [];

        BackToSplashCommand = ReactiveCommand.Create(BackToSplash);
        EditSelectedQuestCommand = ReactiveCommand.Create(EditSelectedQuest);
        SaveAllQuestsCommand = ReactiveCommand.Create(SaveAllQuests);
        SaveIndividualQuestCommand = ReactiveCommand.Create<QuestTemplate>(SaveIndividualQuest);

        // Update the HasQuests property
        this.RaisePropertyChanged(nameof(HasQuests));

        if (HasQuests) {
            MessageService.Info($"Found {QuestTemplates.Count} quests in the packet capture.")
                .WithDuration(TimeSpan.FromSeconds(3))
                .Send();
        }
    }

    private void EditSelectedQuest() {
        if (SelectedQuest is not null) {
            _mainViewModel.OpenQuestEditorInNewTab(SelectedQuest);
        }
    }

    private async void SaveAllQuests() {
        try {
            if (!HasQuests) {
                MessageService.Error("No quests to save.")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
                return;
            }
            
            var window = _mainViewModel.GetMainWindow();
            if (window == null) {
                MessageService.Error("Cannot find main window.")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
                return;
            }

            // Show save options dialog
            var optionsDialog = new QuestSaveOptionsDialog();
            var selectedOption = await optionsDialog.ShowDialog<QuestSaveOption>(window);
            
            var success = selectedOption switch {
                QuestSaveOption.SaveLocal => await SaveAllQuestsLocal(window),
                QuestSaveOption.Upload => await SaveAllQuestsToDatabase(window),
                QuestSaveOption.SaveBoth => await SaveAllQuestsBoth(window),
                _ => false // User cancelled
            };

            if (success && selectedOption != QuestSaveOption.None) {
                var message = selectedOption switch {
                    QuestSaveOption.SaveLocal => $"Successfully saved {QuestTemplates.Count} quests to zip file.",
                    QuestSaveOption.Upload => $"Successfully uploaded {QuestTemplates.Count} quests to database.",
                    QuestSaveOption.SaveBoth => $"Successfully saved {QuestTemplates.Count} quests locally and to database.",
                    _ => ""
                };

                MessageService.Info(message)
                    .WithDuration(TimeSpan.FromSeconds(5))
                    .Send();
            }
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to save quests: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    private async Task<bool> SaveAllQuestsLocal(Avalonia.Controls.Window window) {
        MessageService.Info("Preparing to save quests to zip file...")
            .WithDuration(TimeSpan.FromSeconds(3))
            .Send();
            
        return await QuestZipService.SaveQuestsToZipFileAsync(QuestTemplates, window);
    }

    private async Task<bool> SaveAllQuestsToDatabase(Avalonia.Controls.Window window) {
        MessageService.Info("Uploading quests to database...")
            .WithDuration(TimeSpan.FromSeconds(3))
            .Send();

        return await QuestZipService.SaveQuestsToDatabaseAsync(QuestTemplates, window);
    }

    private async Task<bool> SaveAllQuestsBoth(Avalonia.Controls.Window window) {
        var localSuccess = await SaveAllQuestsLocal(window);
        var databaseSuccess = await SaveAllQuestsToDatabase(window);
        
        return localSuccess || databaseSuccess; // Success if either works
    }

    private async void SaveIndividualQuest(QuestTemplate template) {
        try {
            var window = _mainViewModel.GetMainWindow();
            if (window == null) {
                MessageService.Error("Cannot find main window.")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
                return;
            }

            await QuestZipService.SaveSingleQuestAsync(template, window);
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to save quest: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    private void BackToSplash() 
        => _mainViewModel.ReturnToSplash();

}