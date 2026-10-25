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
using Imview.Core.Common.Constants;
using Imview.Core.Controls.Base;
using Imview.Core.Services;
using ReactiveUI;
using System;

namespace Imview.Core.Controls.Results;

public class ResWaitEditor : EditorWindowBase<ResWait> {
    
    private readonly ResWait _result;
    private readonly NumericUpDown _secondsToWaitInput;

    public ResWaitEditor(ResWait? result = null) 
        : base(result is not null ? "Edit Wait" : "Add Wait") {
        
        _result = result ?? new ResWait();
        
        Width = EditorConstants.DEFAULT_WINDOW_WIDTH;
        Height = 400;
        Background = EditorConstants.DEFAULT_WINDOW_BACKGROUND;
        
        _secondsToWaitInput = new NumericUpDown {
            Value = (decimal) _result.m_secondsToWait,
            Minimum = 0,
            Maximum = uint.MaxValue,
            Increment = 0.5m,
            FormatString = "F2"
        };
        
        InitializeComponent();
    }
    
    private void InitializeComponent() {
        var waitPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = "Seconds to Wait:" },
                _secondsToWaitInput,
                new TextBlock { 
                    Text = "Number of seconds to wait before continuing with the next quest action", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var presetPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { 
                    Text = "Common Wait Times:",
                    FontWeight = Avalonia.Media.FontWeight.Bold
                },
                CreatePresetButton("1 Second", 1),
                CreatePresetButton("3 Seconds", 3),
                CreatePresetButton("5 Seconds", 5),
                CreatePresetButton("10 Seconds", 10),
                CreatePresetButton("30 Seconds", 30),
                CreatePresetButton("1 Minute", 60),
                CreatePresetButton("2 Minutes", 120),
                CreatePresetButton("5 Minutes", 300)
            }
        };
        
        var instructionsPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { 
                    Text = "Wait Instructions:",
                    FontWeight = Avalonia.Media.FontWeight.Bold
                },
                new TextBlock { 
                    Text = "• Wait results pause quest execution for the specified duration", 
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.LightGray
                },
                new TextBlock { 
                    Text = "• Use to create delays between quest actions or dialogue", 
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.LightGray
                },
                new TextBlock { 
                    Text = "• Useful for timing animations, sound effects, or dramatic pauses", 
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.LightGray
                },
                new TextBlock { 
                    Text = "• Can be combined with other results for complex timing scenarios", 
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.LightGray
                },
                new TextBlock { 
                    Text = "• Maximum wait time is over 4 billion seconds (136+ years)", 
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.LightGray
                }
            }
        };
        
        MainPanel.Children.Add(CreateGroupBox("Wait Settings", waitPanel));
        MainPanel.Children.Add(CreateGroupBox("Quick Presets", presetPanel));
        MainPanel.Children.Add(CreateGroupBox("Usage Guidelines", instructionsPanel));
        MainPanel.Children.Add(CreateActionButtons(Save, Cancel));
    }
    
    private Avalonia.Controls.Button CreatePresetButton(string label, uint seconds) {
        var button = new Avalonia.Controls.Button {
            Content = label,
            MinWidth = 100,
            Margin = new Thickness(0, 2)
        };
        
        button.Click += (sender, e) => {
            _secondsToWaitInput.Value = seconds;
        };
        
        return button;
    }
    
    private void Save() {
        try {
            var seconds = (double)(_secondsToWaitInput.Value ?? 0);
            
            if (seconds == 0) {
                MessageService
                    .Info("Wait time must be greater than 0 seconds.")
                    .Send();
                return;
            }
            
            _result.m_secondsToWait = seconds;
            
            ResultSource.SetResult(_result);
            Close();
        }
        catch (Exception ex) {
            MessageService
                .Error($"Failed to save wait result: {ex.Message}")
                .Send();
        }
    }
}