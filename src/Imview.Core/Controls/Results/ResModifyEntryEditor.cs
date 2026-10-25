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
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imview.Core.Common.Constants;
using Imview.Core.Controls.Base;
using Imview.Core.Services;
using ReactiveUI;
using System;

namespace Imview.Core.Controls.Results;

public class ResModifyEntryEditor : EditorWindowBase<ResModifyEntry> {
    
    private readonly ResModifyEntry _result;
    private readonly TextBox _entryNameInput;
    private readonly CheckBox _isQuestRegistryCheckBox;
    private readonly NumericUpDown _valueInput;
    private readonly TextBox _questNameInput;

    public ResModifyEntryEditor(ResModifyEntry? result = null) 
        : base(result is not null ? "Edit Modify Entry" : "Add Modify Entry") {
        
        _result = result ?? new ResModifyEntry();
        
        Width = EditorConstants.DEFAULT_WINDOW_WIDTH;
        Height = 500;
        Background = EditorConstants.DEFAULT_WINDOW_BACKGROUND;
        
        _entryNameInput = new TextBox {
            Text = _result.m_entryName.ToString(),
            Watermark = "Entry name to modify"
        };
        
        _isQuestRegistryCheckBox = new CheckBox {
            IsChecked = _result.m_isQuestRegistry,
            Content = "Is Quest Registry Entry"
        };
        
        _valueInput = new NumericUpDown {
            Value = _result.m_value,
            Minimum = int.MinValue,
            Maximum = int.MaxValue,
            Increment = 1
        };
        
        _questNameInput = new TextBox {
            Text = _result.m_questName != null ? _result.m_questName.ToString() : "",
            Watermark = "Associated quest name"
        };
        
        InitializeComponent();
    }
    
    private void InitializeComponent() {
        var entryPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = "Entry Name:" },
                _entryNameInput,
                new TextBlock { 
                    Text = "The name of the registry entry to modify", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var typePanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                _isQuestRegistryCheckBox,
                new TextBlock { 
                    Text = "When enabled, this modifies a quest registry entry instead of a general registry entry", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray,
                    Margin = new Thickness(20, 0, 0, 0)
                }
            }
        };
        
        var valuePanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = "Value:" },
                _valueInput,
                new TextBlock { 
                    Text = "The value to set for this registry entry", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var questPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = "Quest Name:" },
                _questNameInput,
                new TextBlock { 
                    Text = "The name of the associated quest (if applicable)", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var instructionsPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { 
                    Text = "Modify Entry Instructions:",
                    FontWeight = Avalonia.Media.FontWeight.Bold
                },
                new TextBlock { 
                    Text = "• Registry entries store persistent game state information", 
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.LightGray
                },
                new TextBlock { 
                    Text = "• Quest registry entries are specific to quest progression", 
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.LightGray
                },
                new TextBlock { 
                    Text = "• General registry entries can store any game state data", 
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.LightGray
                },
                new TextBlock { 
                    Text = "• Values can be positive, negative, or zero", 
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.LightGray
                }
            }
        };
        
        MainPanel.Children.Add(CreateGroupBox("Entry Settings", entryPanel));
        MainPanel.Children.Add(CreateGroupBox("Registry Type", typePanel));
        MainPanel.Children.Add(CreateGroupBox("Value Settings", valuePanel));
        MainPanel.Children.Add(CreateGroupBox("Quest Association", questPanel));
        MainPanel.Children.Add(CreateGroupBox("Usage Guidelines", instructionsPanel));
        MainPanel.Children.Add(CreateActionButtons(Save, Cancel));
    }
    
    private void Save() {
        try {
            if (string.IsNullOrWhiteSpace(_entryNameInput.Text)) {
                MessageService
                    .Info("Entry name is required.")
                    .Send();
                return;
            }
            
            _result.m_entryName = _entryNameInput.Text.Trim();
            _result.m_isQuestRegistry = _isQuestRegistryCheckBox.IsChecked ?? false;
            _result.m_value = (int)(_valueInput.Value ?? 0);
            _result.m_questName = _questNameInput.Text?.Trim() ?? string.Empty;
            
            ResultSource.SetResult(_result);
            Close();
        }
        catch (Exception ex) {
            MessageService
                .Error($"Failed to save modify entry result: {ex.Message}")
                .Send();
        }
    }
}