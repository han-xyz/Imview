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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.VisualTree;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Common.Constants;
using Imview.Core.Views;
using Imview.Core.Services;
using Imview.Core.Controls.Results;

namespace Imview.Core.Controls.Templates;

/// <summary>
/// Reusable editor component for TallyCounterTemplate objects.
/// The tally counter controls probabilistic progress tracking for goals - 
/// it determines when goal completion actions should trigger and what rewards to give.
/// For simple goals like "talk to NPC", use count=1 and 100% chance.
/// For collection goals, it controls drop rates and reward distribution.
/// </summary>
public class TallyCounterEditor : UserControl
{
    private TallyCounterTemplate _tallyCounter;
    private string? _questTitle;
    private bool _isEnabled = false;

    // UI Controls
    private CheckBox _enabledCheckBox = null!;
    private NumericUpDown _percentChanceBox = null!;
    private Avalonia.Controls.Button _descriptorButton = null!;
    private TextBlock _descriptorResolvedText = null!;
    private Avalonia.Controls.Button _descriptor2Button = null!;
    private TextBlock _descriptor2ResolvedText = null!;
    private NumericUpDown _countBox = null!;
    private ListBox _tallyResultsList = null!;
    private ObservableCollection<Result> _tallyResults = null!;

    // Values
    private string _descriptorValue = "";
    private string _descriptor2Value = "";

    public TallyCounterTemplate? TallyCounter
    {
        get => _isEnabled ? _tallyCounter : null;
        set
        {
            _tallyCounter = value ?? CreateDefaultTallyCounter();
            _isEnabled = value != null;
            InitializeValues();
        }
    }

    public string? QuestTitle
    {
        get => _questTitle;
        set => _questTitle = value;
    }

    public TallyCounterEditor()
    {
        _tallyCounter = CreateDefaultTallyCounter();
        InitializeComponent();
    }

    private static TallyCounterTemplate CreateDefaultTallyCounter()
    {
        return new TallyCounterTemplate
        {
            m_percentChance = 1.0f, // 100% chance
            m_count = 1,            // Single occurrence
            m_descriptor = "",      // Empty - will be set via locale selection
            m_descriptor2 = "",     // Empty - will be set via locale selection
            m_tallyResults = new ResultList { m_results = new List<Result>() }
        };
    }

    private void InitializeComponent()
    {
        var mainPanel = new StackPanel
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING
        };

        // Enable/Disable checkbox
        _enabledCheckBox = new CheckBox
        {
            Content = "Enable Tally Counter",
            FontWeight = FontWeight.SemiBold
        };
        _enabledCheckBox.IsCheckedChanged += OnEnabledChanged;

        // Description text
        var descriptionText = new TextBlock
        {
            Text = "Tally counters control when rewards are given and actions trigger. " +
                   "Goals from packet captures automatically set this based on the game data. " +
                   "For manually created goals, use count=1 and 100% chance for simple goals like 'talk to NPC'.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray,
            FontStyle = FontStyle.Italic,
            Margin = new Thickness(0, 0, 0, 10)
        };

        // Percent chance control
        _percentChanceBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 100,
            Value = 1.0m,
            FormatString = "0.##",
            Increment = 0.01m
        };

        // Descriptor controls (using locale selection)
        var descriptorPanel = CreateLocaleSelectionPanel(
            "Primary Descriptor:",
            out _descriptorButton,
            out _descriptorResolvedText,
            () => _descriptorValue,
            value => {
                _descriptorValue = value ?? "";
                UpdateDescriptorDisplay(_descriptorButton, _descriptorResolvedText, _descriptorValue);
            }
        );

        var descriptor2Panel = CreateLocaleSelectionPanel(
            "Secondary Descriptor:",
            out _descriptor2Button,
            out _descriptor2ResolvedText,
            () => _descriptor2Value,
            value => {
                _descriptor2Value = value ?? "";
                UpdateDescriptorDisplay(_descriptor2Button, _descriptor2ResolvedText, _descriptor2Value);
            }
        );

        // Count control
        _countBox = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 999999,
            Value = 1
        };

        // Tally Results list
        _tallyResults = new ObservableCollection<Result>();
        _tallyResultsList = new ListBox
        {
            ItemsSource = _tallyResults,
            Height = EditorConstants.DEFAULT_LIST_HEIGHT
        };

        _tallyResultsList.DoubleTapped += async (s, e) => {
            if (_tallyResultsList.SelectedItem is Result selectedResult)
            {
                await EditResult(selectedResult);
            }
        };

        // Create the main content (initially disabled)
        var contentPanel = CreateContentPanel(descriptorPanel, descriptor2Panel, descriptionText);

        // Add controls to main panel
        mainPanel.Children.Add(_enabledCheckBox);
        mainPanel.Children.Add(contentPanel);

        // Wrap in a group box
        var groupBox = CreateGroupBox("Tally Counter", mainPanel);
        Content = groupBox;

        // Initialize values
        InitializeValues();
    }

    private StackPanel CreateContentPanel(Control descriptorPanel, Control descriptor2Panel, Control descriptionText)
    {
        var contentPanel = new StackPanel
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                descriptionText,
                CreateLabeledControl("Trigger Chance (0-1):", _percentChanceBox),
                descriptorPanel,
                descriptor2Panel,
                CreateLabeledControl("Target Count:", _countBox),
                CreateTallyResultsSection()
            }
        };

        return contentPanel;
    }

    private Control CreateTallyResultsSection()
    {
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Margin = new Thickness(0, 0, 0, 5),
            Children = {
                new Avalonia.Controls.Button
                {
                    Content = "Add Result",
                    Command = ReactiveCommand.Create(async () => await AddNewResult())
                },
                new Avalonia.Controls.Button
                {
                    Content = "Remove Result",
                    Command = ReactiveCommand.Create(RemoveSelectedResult)
                }
            }
        };

        var panel = new DockPanel
        {
            LastChildFill = true,
            Children = {
                buttonPanel,
                _tallyResultsList
            }
        };
        DockPanel.SetDock(buttonPanel, Dock.Top);

        return CreateGroupBox("Tally Results", panel);
    }

    private StackPanel CreateLocaleSelectionPanel(
        string labelText,
        out Avalonia.Controls.Button button,
        out TextBlock resolvedText,
        Func<string> getValue,
        Action<string?> setValue)
    {
        var panel = new StackPanel { Spacing = 5 };

        panel.Children.Add(new TextBlock { Text = labelText });

        button = new Avalonia.Controls.Button
        {
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 8),
            MinHeight = 32
        };

        button.Click += async (sender, e) => {
            var preferredCategory = GetTallyCategory();
            var popup = new LocaleSelectionPopup(preferredCategory, getValue());
            var result = await popup.ShowDialog<string?>(GetParentWindow());
            
            if (!string.IsNullOrEmpty(result))
            {
                setValue(result);
            }
        };

        panel.Children.Add(button);

        resolvedText = new TextBlock
        {
            Foreground = Brushes.LightGray,
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        };

        panel.Children.Add(resolvedText);

        return panel;
    }

    private void UpdateDescriptorDisplay(Avalonia.Controls.Button button, TextBlock resolvedText, string value)
    {
        button.Content = string.IsNullOrEmpty(value) ? "(Click to select locale text)" : value;

        if (string.IsNullOrEmpty(value))
        {
            resolvedText.Text = "";
        }
        else
        {
            var resolvedTextValue = ResolveLocaleString(value);
            if (!string.IsNullOrEmpty(resolvedTextValue) && resolvedTextValue != value)
            {
                resolvedText.Text = $"➤ {resolvedTextValue}";
            }
            else
            {
                resolvedText.Text = "➤ (No locale text found)";
            }
        }
    }

    private string? ResolveLocaleString(string localeReference)
    {
        if (string.IsNullOrEmpty(localeReference) || !LocaleService.Instance.IsLoaded)
        {
            return null;
        }

        var underscoreIndex = localeReference.LastIndexOf('_');
        if (underscoreIndex == -1)
        {
            return null;
        }

        var category = localeReference.Substring(0, underscoreIndex);
        var key = localeReference.Substring(underscoreIndex + 1);

        if (key.Length < 8 && key.All(char.IsDigit))
        {
            key = key.PadLeft(8, '0');
        }

        return LocaleService.Instance.GetString(category, key);
    }

    private Avalonia.Controls.Window GetParentWindow()
    {
        return this.FindAncestorOfType<Avalonia.Controls.Window>() ?? 
               (Application.Current?.ApplicationLifetime as 
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow!;
    }

    private string? GetTallyCategory()
    {
        if (string.IsNullOrEmpty(_questTitle))
        {
            return null;
        }

        // Extract ID from quest title like "QuestTitle_1625BF" -> "1625BF"
        var underscoreIndex = _questTitle.LastIndexOf('_');
        if (underscoreIndex == -1)
        {
            return null;
        }

        var questId = _questTitle.Substring(underscoreIndex + 1);
        return $"WizQst{questId}";
    }


    private void OnEnabledChanged(object? sender, EventArgs e)
    {
        _isEnabled = _enabledCheckBox.IsChecked ?? false;
        
        // Enable/disable all content controls
        if (Content is Border groupBox && groupBox.Child is StackPanel mainPanel)
        {
            if (mainPanel.Children.Count > 1 && mainPanel.Children[1] is StackPanel contentPanel)
            {
                contentPanel.IsEnabled = _isEnabled;
            }
        }
    }

    private void InitializeValues()
    {
        _enabledCheckBox.IsChecked = _isEnabled;

        _percentChanceBox.Value = (decimal)_tallyCounter.m_percentChance;
        _descriptorValue = _tallyCounter.m_descriptor.ToString();
        _descriptor2Value = _tallyCounter.m_descriptor2.ToString();
        _countBox.Value = _tallyCounter.m_count;

        _tallyResults.Clear();
        var results = _tallyCounter.m_tallyResults?.m_results ?? new List<Result>();
        foreach (var result in results)
        {
            _tallyResults.Add(result);
        }

        // Update descriptor displays
        UpdateDescriptorDisplay(_descriptorButton, _descriptorResolvedText, _descriptorValue);
        UpdateDescriptorDisplay(_descriptor2Button, _descriptor2ResolvedText, _descriptor2Value);

        // Apply enabled state
        OnEnabledChanged(null, EventArgs.Empty);
    }

    private async System.Threading.Tasks.Task EditResult(Result result)
    {
        var editor = new ResultTemplateEditor(result);
        await editor.ShowDialog(GetParentWindow());

        var editedResult = await editor.GetResultAsync();
        if (editedResult != null)
        {
            var index = _tallyResults.IndexOf(result);
            _tallyResults[index] = editedResult;
        }
    }

    private async System.Threading.Tasks.Task AddNewResult()
    {
        var editor = new ResultTemplateEditor();
        await editor.ShowDialog(GetParentWindow());

        var result = await editor.GetResultAsync();
        if (result != null)
        {
            _tallyResults.Add(result);
        }
    }

    private void RemoveSelectedResult()
    {
        if (_tallyResultsList.SelectedItem is Result selectedResult)
        {
            _tallyResults.Remove(selectedResult);
        }
    }

    /// <summary>
    /// Saves the current UI state back to the TallyCounter object
    /// </summary>
    public void SaveToTallyCounter()
    {
        if (_isEnabled)
        {
            _tallyCounter.m_percentChance = (float)(_percentChanceBox.Value ?? 1.0m);
            _tallyCounter.m_descriptor = _descriptorValue;
            _tallyCounter.m_descriptor2 = _descriptor2Value;
            _tallyCounter.m_count = (int)(_countBox.Value ?? 1);
            _tallyCounter.m_tallyResults = new ResultList { m_results = _tallyResults.ToList() };
        }
    }

    private static Control CreateLabeledControl(string labelText, Control control)
        => new StackPanel
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                new TextBlock { Text = labelText },
                control
            }
        };

    private static Border CreateGroupBox(string header, Control content)
        => new()
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = EditorConstants.DEFAULT_CORNER_RADIUS,
            Padding = EditorConstants.DEFAULT_GROUP_PADDING,
            Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel
            {
                Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
                Children = {
                    new TextBlock
                    {
                        Text = header,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 0, 0, 5)
                    },
                    content
                }
            }
        };
}