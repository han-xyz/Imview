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

public class ResPlaySoundEditor : EditorWindowBase<ResPlaySound> {
    
    private readonly ResPlaySound _result;
    private readonly TextBox _soundNameInput;
    private readonly CheckBox _blockingCheckBox;
    private readonly NumericUpDown _reinteractTimeInput;
    
    // ZoneRouter controls
    private readonly NumericUpDown _locXInput;
    private readonly NumericUpDown _locYInput;
    private readonly NumericUpDown _locZInput;
    private readonly ComboBox _routingTypeComboBox;
    private readonly CheckBox _useLocationCheckBox;
    private readonly CheckBox _useTriggerLocationCheckBox;

    public ResPlaySoundEditor(ResPlaySound? result = null) 
        : base(result is not null ? "Edit Play Sound" : "Add Play Sound") {
        
        _result = result ?? new ResPlaySound();
        
        Width = EditorConstants.DEFAULT_WINDOW_WIDTH;
        Height = 700;
        Background = EditorConstants.DEFAULT_WINDOW_BACKGROUND;
        
        _soundNameInput = new TextBox {
            Text = _result.m_soundName.ToString(),
            Watermark = "Sound file name or path"
        };
        
        _blockingCheckBox = new CheckBox {
            IsChecked = _result.m_blocking,
            Content = "Blocking Sound"
        };
        
        _reinteractTimeInput = new NumericUpDown {
            Value = (decimal)_result.m_reinteractTime,
            Minimum = 0,
            Maximum = 9999,
            Increment = 0.1m,
            FormatString = "F1"
        };
        
        // ZoneRouter controls
        var router = _result.m_router ?? new ZoneRouter();
        
        _locXInput = new NumericUpDown {
            Value = (decimal)router.m_locX,
            Minimum = -9999,
            Maximum = 9999,
            Increment = 0.1m,
            FormatString = "F2"
        };
        
        _locYInput = new NumericUpDown {
            Value = (decimal)router.m_locY,
            Minimum = -9999,
            Maximum = 9999,
            Increment = 0.1m,
            FormatString = "F2"
        };
        
        _locZInput = new NumericUpDown {
            Value = (decimal)router.m_locZ,
            Minimum = -9999,
            Maximum = 9999,
            Increment = 0.1m,
            FormatString = "F2"
        };
        
        _routingTypeComboBox = new ComboBox {
            ItemsSource = new[] { "ROUTING_ACTOR", "ROUTING_ZONE", "ROUTING_PROXIMITY" },
            SelectedIndex = (int)router.m_routingType
        };
        
        _useLocationCheckBox = new CheckBox {
            IsChecked = router.m_useLocation,
            Content = "Use Location"
        };
        
        _useTriggerLocationCheckBox = new CheckBox {
            IsChecked = router.m_useTriggerLocation,
            Content = "Use Trigger Location"
        };
        
        InitializeComponent();
    }
    
    private void InitializeComponent() {
        var soundPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = "Sound Name:" },
                _soundNameInput,
                new TextBlock { 
                    Text = "Name or path of the sound file to play", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var behaviorPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                _blockingCheckBox,
                new TextBlock { 
                    Text = "When enabled, this sound blocks other actions until complete", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray,
                    Margin = new Thickness(20, 0, 0, 10)
                },
                new TextBlock { Text = "Reinteract Time (seconds):" },
                _reinteractTimeInput,
                new TextBlock { 
                    Text = "Time before player can interact again after sound starts", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var locationPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = "Location X:" },
                _locXInput,
                new TextBlock { Text = "Location Y:" },
                _locYInput,
                new TextBlock { Text = "Location Z:" },
                _locZInput,
                new TextBlock { 
                    Text = "3D coordinates where the sound should be played", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var routingPanel = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = "Routing Type:" },
                _routingTypeComboBox,
                new TextBlock { 
                    Text = "How the sound should be routed to players", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                },
                _useLocationCheckBox,
                new TextBlock { 
                    Text = "Use the specified location coordinates", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray,
                    Margin = new Thickness(20, 0, 0, 5)
                },
                _useTriggerLocationCheckBox,
                new TextBlock { 
                    Text = "Use the trigger location instead of specified coordinates", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray,
                    Margin = new Thickness(20, 0, 0, 0)
                }
            }
        };
        
        MainPanel.Children.Add(CreateGroupBox("Sound Settings", soundPanel));
        MainPanel.Children.Add(CreateGroupBox("Behavior Settings", behaviorPanel));
        MainPanel.Children.Add(CreateGroupBox("Location Settings", locationPanel));
        MainPanel.Children.Add(CreateGroupBox("Routing Settings", routingPanel));
        MainPanel.Children.Add(CreateActionButtons(Save, Cancel));
    }
    
    private void Save() {
        try {
            _result.m_soundName = _soundNameInput.Text ?? string.Empty;
            _result.m_blocking = _blockingCheckBox.IsChecked ?? false;
            _result.m_reinteractTime = (float)(_reinteractTimeInput.Value ?? 0);
            
            // Create or update ZoneRouter
            _result.m_router = new ZoneRouter {
                m_locX = (float)(_locXInput.Value ?? 0),
                m_locY = (float)(_locYInput.Value ?? 0),
                m_locZ = (float)(_locZInput.Value ?? 0),
                // m_routingType = (RoutingType)(_routingTypeComboBox.SelectedIndex), // Will use default
                m_useLocation = _useLocationCheckBox.IsChecked ?? false,
                m_useTriggerLocation = _useTriggerLocationCheckBox.IsChecked ?? false
            };
            
            ResultSource.SetResult(_result);
            Close();
        }
        catch (Exception ex) {
            MessageService
                .Error($"Failed to save play sound result: {ex.Message}")
                .Send();
        }
    }
}