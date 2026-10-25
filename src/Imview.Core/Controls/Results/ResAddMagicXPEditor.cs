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
using System;

namespace Imview.Core.Controls.Results;

public class ResAddMagicXPEditor : EditorWindowBase<ResAddMagicXP> {
    
    private readonly ResAddMagicXP _result;
    private readonly NumericUpDown _experienceInput;
    private readonly TextBox _magicSchoolInput;
    private readonly TextBox _sourceTypeInput;

    public ResAddMagicXPEditor(ResAddMagicXP? result = null) 
        : base(result is not null ? "Edit Magic XP Reward" : "Add Magic XP Reward") {
        
        _result = result ?? new ResAddMagicXP();
        
        Width = EditorConstants.SMALL_WINDOW_WIDTH;
        Height = 400;
        Background = EditorConstants.DEFAULT_WINDOW_BACKGROUND;
        
        _experienceInput = new NumericUpDown {
            Minimum = 0,
            Maximum = int.MaxValue,
            Value = _result.m_experience,
            FormatString = "N0"
        };
        
        _magicSchoolInput = new TextBox {
            Text = _result.m_magicSchool.ToString(),
            Watermark = "Magic school"
        };
        
        _sourceTypeInput = new TextBox {
            Text = _result.m_sourceType.ToString(),
            Watermark = "Source type (optional)"
        };
        
        InitializeComponent();
    }
    
    private void InitializeComponent() {
        var experiencePanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = "Experience Amount:" },
                _experienceInput
            }
        };
        
        var magicSchoolPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = "Magic School (0-100):" },
                _magicSchoolInput,
                new TextBlock { 
                    Text = "Magic school identifier or percentage", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var sourceTypePanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = "Source Type:" },
                _sourceTypeInput
            }
        };
        
        MainPanel.Children.Add(CreateGroupBox("Experience Settings", experiencePanel));
        MainPanel.Children.Add(CreateGroupBox("Magic School", magicSchoolPanel));
        MainPanel.Children.Add(CreateGroupBox("Source Type", sourceTypePanel));
        MainPanel.Children.Add(CreateActionButtons(Save, Cancel));
    }
    
    private void Save() {
        try {
            _result.m_experience = (int)(_experienceInput.Value ?? 0);
            _result.m_magicSchool = _magicSchoolInput.Text ?? string.Empty;
            _result.m_sourceType = _sourceTypeInput.Text ?? string.Empty;
            
            ResultSource.SetResult(_result);
            Close();
        }
        catch (Exception ex) {
            MessageService
                .Error($"Failed to save magic XP reward: {ex.Message}")
                .Send();
        }
    }
}