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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Common.Constants;
using Imview.Core.Common.Utils;
using System.Threading.Tasks;
using Imview.Core.Controls.Base;
using Imview.Core.Common.Extensions;

namespace Imview.Core.Controls;

/// <summary>
/// Editor for GoalCompleteLogic which defines how goals are combined to progress the quest.
/// </summary>
public class GoalCompleteLogicEditor : EditorWindowBase<GoalCompleteLogic> {
    
    private readonly GoalCompleteLogic _logic;
    private readonly List<string> _availableGoals;

    // UI Controls
    private ListBox _goalsAndListBox;
    private ListBox _goalsOrListBox;
    private ListBox _goalsToAddListBox;
    private NumericUpDown _requiredOrCountBox;
    private CheckBox _completeQuestBox;
    private ComboBox _availableGoalsBox;

    // Observable collections for the list boxes
    private readonly ObservableCollection<string> _goalsAnd;
    private readonly ObservableCollection<string> _goalsOr;
    private readonly ObservableCollection<string> _goalsToAdd;

    public GoalCompleteLogicEditor(GoalCompleteLogic? logic = null, List<string>? availableGoals = null)
        : base("Edit Goal Completion Logic") {
        _logic = logic ?? new GoalCompleteLogic();
        _availableGoals = availableGoals ?? [];

        // Initialize collections
        _goalsAnd = new ObservableCollection<string>(_logic.m_goalsAND?.Select(g => g.ToString()) ?? []);
        _goalsOr = new ObservableCollection<string>(_logic.m_goalsOR?.Select(g => g.ToString()) ?? []);
        _goalsToAdd = new ObservableCollection<string>(_logic.m_goalsToAdd?.Select(g => g.ToString()) ?? []);

        _goalsAndListBox = new ListBox();
        _goalsOrListBox = new ListBox();
        _goalsToAddListBox = new ListBox();
        _requiredOrCountBox = new NumericUpDown();
        _completeQuestBox = new CheckBox();
        _availableGoalsBox = new ComboBox();

        InitializeComponent();
    }

    private void InitializeComponent() {
        var mainPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Margin = EditorConstants.DEFAULT_MARGIN_THICKNESS
        };

        InitializeControls();

        // Add control sections to the main panel.
        mainPanel.Children.Add(CreateAvailableGoalsSection());
        mainPanel.Children.Add(CreateAndGoalsSection());
        mainPanel.Children.Add(CreateOrGoalsSection());
        mainPanel.Children.Add(CreateGoalsToAddSection());
        mainPanel.Children.Add(CreateQuestCompletionSection());
        mainPanel.Children.Add(CreateActionButtons());

        Content = new ScrollViewer { Content = mainPanel };
    }

    private void InitializeControls() {
        // Create list boxes for goals.
        _goalsAndListBox = new ListBox {
            ItemsSource = _goalsAnd,
            Height = 120
        };

        _goalsOrListBox = new ListBox {
            ItemsSource = _goalsOr,
            Height = 120
        };

        _goalsToAddListBox = new ListBox {
            ItemsSource = _goalsToAdd,
            Height = 120
        };

        // Create combo box for available goals.
        _availableGoalsBox = new ComboBox {
            ItemsSource = _availableGoals,
            IsTextSearchEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Create other controls.
        _requiredOrCountBox = new NumericUpDown {
            Minimum = 1,
            Maximum = 100,
            Value = _logic.m_requiredORCount > 0 ? _logic.m_requiredORCount : 1
        };

        _completeQuestBox = new CheckBox {
            Content = "Complete Quest when logic satisfied",
            IsChecked = _logic.m_completeQuest
        };
    }

    private Control CreateAvailableGoalsSection() {
        var content = new DockPanel {
            LastChildFill = true,
            Children = {
                new TextBlock {
                    Text = "Select a goal:",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                },
                _availableGoalsBox
            }
        };
        DockPanel.SetDock(content.Children[0], Dock.Left);

        return CreateGroupBox("Available Goals", content);
    }

    private Control CreateAndGoalsSection() {
        var buttonPanel = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Margin = new Thickness(0, 0, 0, 5),
            Children = {
                new Avalonia.Controls.Button {
                    Content = "Add Selected Goal",
                    Command = ReactiveCommand.Create(AddGoalToAnd)
                },
                new Avalonia.Controls.Button {
                    Content = "Remove Selected",
                    Command = ReactiveCommand.Create(() => RemoveSelectedItem(_goalsAndListBox, _goalsAnd))
                }
            }
        };

        var panel = new DockPanel {
            LastChildFill = true,
            Children = {
                buttonPanel,
                _goalsAndListBox
            }
        };
        DockPanel.SetDock(buttonPanel, Dock.Top);

        var infoText = new TextBlock {
            Text = "All goals in this list must be completed (AND logic).",
            FontStyle = FontStyle.Italic,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 5, 0, 0)
        };

        var content = new StackPanel {
            Children = {
                panel,
                infoText
            }
        };

        return CreateGroupBox("Required Goals (AND)", content);
    }

    private Control CreateOrGoalsSection() {
        var requiredCountPanel = new DockPanel {
            LastChildFill = true,
            Margin = new Thickness(0, 0, 0, 5),
            Children = {
                new TextBlock {
                    Text = "Required number to complete:",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                },
                _requiredOrCountBox
            }
        };
        DockPanel.SetDock(requiredCountPanel.Children[0], Dock.Left);

        var buttonPanel = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Margin = new Thickness(0, 0, 0, 5),
            Children = {
                new Avalonia.Controls.Button {
                    Content = "Add Selected Goal",
                    Command = ReactiveCommand.Create(AddGoalToOr)
                },
                new Avalonia.Controls.Button {
                    Content = "Remove Selected",
                    Command = ReactiveCommand.Create(() => RemoveSelectedItem(_goalsOrListBox, _goalsOr))
                }
            }
        };

        var panel = new DockPanel {
            LastChildFill = true,
            Children = {
                buttonPanel,
                _goalsOrListBox
            }
        };
        DockPanel.SetDock(buttonPanel, Dock.Top);

        var infoText = new TextBlock {
            Text = "Only the specified number of goals in this list need to be completed (OR logic).",
            FontStyle = FontStyle.Italic,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 5, 0, 0)
        };

        var content = new StackPanel {
            Children = {
                requiredCountPanel,
                panel,
                infoText
            }
        };

        return CreateGroupBox("Optional Goals (OR)", content);
    }

    private Control CreateGoalsToAddSection() {
        var buttonPanel = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Margin = new Thickness(0, 0, 0, 5),
            Children = {
                new Avalonia.Controls.Button {
                    Content = "Add Selected Goal",
                    Command = ReactiveCommand.Create(AddGoalToAdd)
                },
                new Avalonia.Controls.Button {
                    Content = "Remove Selected",
                    Command = ReactiveCommand.Create(() => RemoveSelectedItem(_goalsToAddListBox, _goalsToAdd))
                }
            }
        };

        var panel = new DockPanel {
            LastChildFill = true,
            Children = {
                buttonPanel,
                _goalsToAddListBox
            }
        };
        DockPanel.SetDock(buttonPanel, Dock.Top);

        var infoText = new TextBlock {
            Text = "Goals to add when the logic is satisfied.",
            FontStyle = FontStyle.Italic,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 5, 0, 0)
        };

        var content = new StackPanel {
            Children = {
                panel,
                infoText
            }
        };

        return CreateGroupBox("Goals to Add on Completion", content);
    }

    private Control CreateQuestCompletionSection() {
        var content = new StackPanel {
            Children = {
                _completeQuestBox,
                new TextBlock {
                    Text = "If checked, the quest will be marked as complete when this logic is satisfied.",
                    FontStyle = FontStyle.Italic,
                    Foreground = Brushes.LightGray,
                    Margin = new Thickness(0, 5, 0, 0)
                }
            }
        };

        return CreateGroupBox("Quest Completion", content);
    }

    private Control CreateActionButtons() => new StackPanel {
        Orientation = Orientation.Horizontal,
        Spacing = 10,
        HorizontalAlignment = HorizontalAlignment.Right,
        Margin = new Thickness(0, 10, 0, 0),
        Children = {
                new Avalonia.Controls.Button {
                    Content = "Save",
                    Command = ReactiveCommand.Create(Save)
                },
                new Avalonia.Controls.Button {
                    Content = "Cancel",
                    Command = ReactiveCommand.Create(Cancel)
                }
            }
    };

    private static new Border CreateGroupBox(string header, Control content) => new() {
        BorderBrush = Brushes.Gray,
        BorderThickness = new Thickness(1),
        CornerRadius = EditorConstants.DEFAULT_CORNER_RADIUS,
        Padding = EditorConstants.DEFAULT_GROUP_PADDING,
        Margin = new Thickness(0, 0, 0, 10),
        Child = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                    new TextBlock {
                        Text = header,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 0, 0, 5)
                    },
                    content
                }
        }
    };

    private void AddGoalToAnd() {
        if (_availableGoalsBox.SelectedItem is string selectedGoal && !string.IsNullOrEmpty(selectedGoal)) {
            if (!_goalsAnd.Contains(selectedGoal)) {
                _goalsAnd.Add(selectedGoal);
            }
        }
    }

    private void AddGoalToOr() {
        if (_availableGoalsBox.SelectedItem is string selectedGoal && !string.IsNullOrEmpty(selectedGoal)) {
            if (!_goalsOr.Contains(selectedGoal)) {
                _goalsOr.Add(selectedGoal);
            }
        }
    }

    private void AddGoalToAdd() {
        if (_availableGoalsBox.SelectedItem is string selectedGoal && !string.IsNullOrEmpty(selectedGoal)) {
            if (!_goalsToAdd.Contains(selectedGoal)) {
                _goalsToAdd.Add(selectedGoal);
            }
        }
    }

    private void RemoveSelectedItem(ListBox listBox, ObservableCollection<string> collection) {
        if (listBox.SelectedItem is string selectedItem) {
            collection.Remove(selectedItem);
        }
    }

    private void Save() {
        try {
            // Convert string collections to ByteString lists
            _logic.m_goalsAND = _goalsAnd.ToByteStrings();
            _logic.m_goalsOR = _goalsOr.ToByteStrings();
            _logic.m_goalsToAdd = _goalsToAdd.ToByteStrings();

            // Set other properties
            _logic.m_requiredORCount = (int) (_requiredOrCountBox.Value ?? 1);
            _logic.m_completeQuest = _completeQuestBox.IsChecked ?? false;

            ResultSource.SetResult(_logic);
            Close();
        }
        catch (Exception ex) {
            // TODO: Show error dialog
            Console.WriteLine($"Error saving goal logic: {ex}");
        }
    }

    protected override void Cancel() {
        ResultSource.SetResult(_logic);
        Close();
    }

    /// <summary>
    /// Gets the result of the editor.
    /// </summary>
    public override Task<GoalCompleteLogic> GetResultAsync() => ResultSource.Task;
    
}