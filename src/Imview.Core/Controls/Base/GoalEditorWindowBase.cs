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

using Avalonia.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Common.Utils;
using Imview.Core.Common.Constants;
using Imcodec.IO;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI;
using Avalonia.Layout;
using System.Collections.Generic;
using Imview.Core.Controls.Results;
using Imview.Core.Controls.Templates;
using Imview.Core.Common.Extensions;

namespace Imview.Core.Controls.Base;

/// <summary>
/// Base class for all goal editor windows.
/// </summary>
public abstract class GoalEditorWindowBase : EditorWindowBase<GoalTemplate> {

    protected new readonly GoalTemplate Template;

    // Common controls
    protected TextBox GoalNameBox;
    protected NumericUpDown GoalNameIdBox;
    protected TextBox GoalTitleBox;
    protected TextBox LocationNameBox;
    protected TextBox DisplayImage1Box;
    protected TextBox DisplayImage2Box;
    protected TextBox DestinationZoneBox;
    protected TextBox ClientTagsBox;
    protected TextBox GenericEventsBox;
    protected ComboBox GoalTypeBox;
    protected CheckBox AutoQualifyBox;
    protected CheckBox AutoCompleteBox;
    protected CheckBox NoQuestHelperBox;
    protected CheckBox PetOnlyQuestBox;
    protected CheckBox HideGoalFloatyTextBox;
    protected QuestDialogEditor DialogEditor;
    public TallyCounterEditor TallyCounterEditor;

    // Results lists
    protected ListBox CompletionResultsBox;
    protected ListBox ActivationResultsBox;
    protected ObservableCollection<Result> CompletionResults;
    protected ObservableCollection<Result> ActivationResults;

    protected GoalEditorWindowBase(GoalTemplate template, string title) : base(title) {
        Width = 800;
        
        GoalNameBox = new TextBox();
        GoalNameIdBox = new NumericUpDown();
        GoalTitleBox = new TextBox();
        LocationNameBox = new TextBox();
        DisplayImage1Box = new TextBox();
        DisplayImage2Box = new TextBox();
        DestinationZoneBox = new TextBox();
        ClientTagsBox = new TextBox();
        GenericEventsBox = new TextBox();
        GoalTypeBox = new ComboBox();
        AutoQualifyBox = new CheckBox();
        AutoCompleteBox = new CheckBox();
        NoQuestHelperBox = new CheckBox();
        PetOnlyQuestBox = new CheckBox();
        HideGoalFloatyTextBox = new CheckBox();
        DialogEditor = new QuestDialogEditor();
        TallyCounterEditor = new TallyCounterEditor();
        CompletionResultsBox = new ListBox();
        ActivationResultsBox = new ListBox();
        CompletionResults = [];
        ActivationResults = [];
        Template = template ?? throw new ArgumentNullException(nameof(template));

        InitializeBaseControls();
        InitializeValues();
        InitializeResultsLists();
    }

    private void InitializeBaseControls() {
        GoalNameBox = new TextBox();
        GoalNameIdBox = new NumericUpDown();
        GoalTitleBox = new TextBox();
        LocationNameBox = new TextBox();
        DisplayImage1Box = new TextBox();
        DisplayImage2Box = new TextBox();
        DestinationZoneBox = new TextBox();
        ClientTagsBox = new TextBox();
        GenericEventsBox = new TextBox();

        GoalTypeBox = new ComboBox {
            ItemsSource = Enum.GetValues<GOAL_TYPE>()
        };

        AutoQualifyBox = new CheckBox { Content = "Auto Qualify" };
        AutoCompleteBox = new CheckBox { Content = "Auto Complete" };
        NoQuestHelperBox = new CheckBox { Content = "No Quest Helper" };
        PetOnlyQuestBox = new CheckBox { Content = "Pet Only Quest" };
        HideGoalFloatyTextBox = new CheckBox { Content = "Hide Goal Floaty Text" };
    }

    private void InitializeResultsLists() {
        CompletionResults = new ObservableCollection<Result>(
            Template.m_completeResults?.m_results ?? new List<Result>()
        );

        ActivationResults = new ObservableCollection<Result>(
            Template.m_activateResults?.m_results ?? new List<Result>()
        );

        CompletionResultsBox = new ListBox {
            ItemsSource = CompletionResults,
            Height = EditorConstants.DEFAULT_LIST_HEIGHT
        };

        ActivationResultsBox = new ListBox {
            ItemsSource = ActivationResults,
            Height = EditorConstants.DEFAULT_LIST_HEIGHT
        };

        CompletionResultsBox.DoubleTapped += async (s, e) => {
            if (CompletionResultsBox.SelectedItem is Result selectedResult) {
                await EditResult(selectedResult, CompletionResults);
            }
        };

        ActivationResultsBox.DoubleTapped += async (s, e) => {
            if (ActivationResultsBox.SelectedItem is Result selectedResult) {
                await EditResult(selectedResult, ActivationResults);
            }
        };
    }

    protected virtual void InitializeValues() {
        GoalNameBox.Text = Template.m_goalName.ToString();
        GoalNameIdBox.Value = Template.m_goalNameID;
        GoalTitleBox.Text = Template.m_goalTitle.ToString();
        LocationNameBox.Text = Template.m_locationName.ToString();
        DisplayImage1Box.Text = Template.m_displayImage1.ToString();
        DisplayImage2Box.Text = Template.m_displayImage2.ToString();
        DestinationZoneBox.Text = Template.m_destinationZone.ToString();
        ClientTagsBox.Text = Template.m_clientTags?.ToCommaSeparatedString();
        GenericEventsBox.Text = Template.m_genericEvents?.ToCommaSeparatedString();

        AutoQualifyBox.IsChecked = Template.m_autoQualify;
        AutoCompleteBox.IsChecked = Template.m_autoComplete;
        NoQuestHelperBox.IsChecked = Template.m_noQuestHelper;
        PetOnlyQuestBox.IsChecked = Template.m_petOnlyQuest;
        HideGoalFloatyTextBox.IsChecked = Template.m_hideGoalFloatyText;

        GoalTypeBox.SelectedItem = Template.m_goalType;
        
        // Initialize dialog editor with goal's dialog list
        DialogEditor.DialogList = Template.m_dialogList as ActorDialogList;
        
        // Initialize tally counter editor - handle enabled/disabled state
        if (Template.m_tallyCounter == null)
        {
            // For manually created goals, create a default disabled tally counter
            var defaultTallyCounter = new TallyCounterTemplate
            {
                m_percentChance = 1.0f, // 100% chance for new goals
                m_count = 1,            // Single occurrence
                m_descriptor = "",      
                m_descriptor2 = "",     
                m_tallyResults = new ResultList { m_results = new List<Result>() }
            };
            // Set it to null to indicate disabled state in the editor
            TallyCounterEditor.TallyCounter = null;
        }
        else
        {
            // Tally counter exists, so it should be enabled
            TallyCounterEditor.TallyCounter = Template.m_tallyCounter;
        }
    }

    protected virtual void SaveValues() {
        try {
            Template.m_goalName = new ByteString(GoalNameBox.Text ?? string.Empty);
            Template.m_goalNameID = (uint) (GoalNameIdBox.Value ?? 0);
            Template.m_goalTitle = new ByteString(GoalTitleBox.Text ?? string.Empty);
            Template.m_locationName = new ByteString(LocationNameBox.Text ?? string.Empty);
            Template.m_displayImage1 = new ByteString(DisplayImage1Box.Text ?? string.Empty);
            Template.m_displayImage2 = new ByteString(DisplayImage2Box.Text ?? string.Empty);
            Template.m_destinationZone = new ByteString(DestinationZoneBox.Text ?? string.Empty);

            Template.m_clientTags = (ClientTagsBox.Text?.SplitCommaSeparated() ?? []).ToByteStrings();
            Template.m_genericEvents = (GenericEventsBox.Text?.SplitCommaSeparated() ?? []).ToByteStrings();

            Template.m_autoQualify = AutoQualifyBox.IsChecked ?? false;
            Template.m_autoComplete = AutoCompleteBox.IsChecked ?? false;
            Template.m_noQuestHelper = NoQuestHelperBox.IsChecked ?? false;
            Template.m_petOnlyQuest = PetOnlyQuestBox.IsChecked ?? false;
            Template.m_hideGoalFloatyText = HideGoalFloatyTextBox.IsChecked ?? false;

            Template.m_goalType = (GOAL_TYPE) (GoalTypeBox.SelectedItem ?? Template.m_goalType);

            // Save dialog system - convert from editor back to ActorDialogList
            Template.m_dialogList = DialogEditor.ToActorDialogList();

            // Save tally counter - only save if enabled
            TallyCounterEditor.SaveToTallyCounter();
            Template.m_tallyCounter = TallyCounterEditor.TallyCounter;

            Template.m_completeResults = new ResultList { m_results = CompletionResults.ToList() };
            Template.m_activateResults = new ResultList { m_results = ActivationResults.ToList() };
        }
        catch (Exception ex) {
            // TODO: Replace with proper logging and error handling
            Console.WriteLine($"Error saving values: {ex}");
            throw;
        }
    }

    protected async Task EditResult(Result result, ObservableCollection<Result> resultsList) {
        var editor = new ResultTemplateEditor(result);
        await editor.ShowDialog(this);

        var editedResult = await editor.GetResultAsync();
        if (editedResult != null) {
            var index = resultsList.IndexOf(result);
            resultsList[index] = editedResult;
        }
    }

    protected async Task AddNewResult(ObservableCollection<Result> resultsList) {
        var editor = new ResultTemplateEditor();
        await editor.ShowDialog(this);

        var result = await editor.GetResultAsync();
        if (result != null) {
            resultsList.Add(result);
        }
    }

    protected StackPanel CreateBaseControlsPanel() {
        var basicInfoContent = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                CreateLabeledControl("Goal Name:", GoalNameBox),
                CreateLabeledControl("Goal Name ID:", GoalNameIdBox),
                CreateLabeledControl("Goal Title:", GoalTitleBox),
                CreateLabeledControl("Goal Type:", GoalTypeBox)
            }
        };


        var locationContent = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                CreateLabeledControl("Location Name:", LocationNameBox),
                CreateLabeledControl("Destination Zone:", DestinationZoneBox),
                CreateLabeledControl("Display Image 1:", DisplayImage1Box),
                CreateLabeledControl("Display Image 2:", DisplayImage2Box)
            }
        };

        var tagsContent = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                CreateLabeledControl("Client Tags (comma-separated):", ClientTagsBox),
                CreateLabeledControl("Generic Events (comma-separated):", GenericEventsBox)
            }
        };

        var flagsContent = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                AutoQualifyBox,
                AutoCompleteBox,
                NoQuestHelperBox,
                PetOnlyQuestBox,
                HideGoalFloatyTextBox
            }
        };

        MainPanel.Children.Add(CreateGroupBox("Basic Information", basicInfoContent));
        MainPanel.Children.Add(CreateGroupBox("Goal Dialogue", DialogEditor));
        MainPanel.Children.Add(CreateGroupBox("Location and Display", locationContent));
        MainPanel.Children.Add(CreateGroupBox("Tags and Events", tagsContent));
        MainPanel.Children.Add(CreateGroupBox("Flags", flagsContent));

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        AddResultsPanel("Activation Results", ActivationResultsBox,
            () => AddNewResult(ActivationResults),
            () => RemoveSelectedResult(ActivationResultsBox, ActivationResults));

        AddResultsPanel("Completion Results", CompletionResultsBox,
            () => AddNewResult(CompletionResults),
            () => RemoveSelectedResult(CompletionResultsBox, CompletionResults));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        // Add tally counter at the bottom
        MainPanel.Children.Add(TallyCounterEditor);

        return MainPanel;
    }

    private void AddResultsPanel(string title, ListBox listBox, System.Action addAction, System.Action removeAction) {
        var buttonPanel = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Margin = new Thickness(0, 0, 0, 5),
            Children = {
                new Avalonia.Controls.Button { Content = "Add Result", Command = ReactiveCommand.Create(addAction) },
                new Avalonia.Controls.Button { Content = "Remove Result", Command = ReactiveCommand.Create(removeAction) }
            }
        };

        var panel = new DockPanel {
            LastChildFill = true,
            Children = {
                buttonPanel,
                listBox
            }
        };
        DockPanel.SetDock(buttonPanel, Dock.Top);

        MainPanel.Children.Add(CreateGroupBox(title, panel));
    }

    private void RemoveSelectedResult(ListBox listBox, ObservableCollection<Result> resultsList) {
        if (listBox.SelectedItem is Result selectedResult) {
            resultsList.Remove(selectedResult);
        }
    }

    protected virtual void Save() {
        try {
            SaveValues();
            ResultSource.SetResult(Template);
            Close();
        }
        catch (Exception ex) {
            // TODO: Replace with proper error handling dialog
            Console.WriteLine($"Error saving template: {ex}");
        }
    }

}