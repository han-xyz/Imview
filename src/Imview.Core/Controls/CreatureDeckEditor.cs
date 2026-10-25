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
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Imview.Core.Common.Constants;
using Imview.Core.Data;
using Imview.Core.Models;
using Imview.Core.Services;

namespace Imview.Core.Controls;

/// <summary>
/// Control for editing creature spell decks
/// Based exactly on the NpcInventoryEditor pattern
/// </summary>
public class CreatureDeckEditor : Window
{
    private readonly string _deckName;
    private readonly string _creatureName;
    private readonly ObservableCollection<uint> _spellTemplateIds = new();
    private bool _isDirty;
    
    private ListBox _spellListBox = null!;
    private TextBox _addSpellTextBox = null!;
    private Button _addSpellButton = null!;
    private Button _removeSpellButton = null!;
    private Button _saveButton = null!;
    private Button _cancelButton = null!;

    public bool WasSaved { get; private set; }

    public CreatureDeckEditor(string deckName, string creatureName, CreatureDeck? existingDeck = null)
    {
        _deckName = deckName;
        _creatureName = creatureName;
        
        Title = $"Creature Deck Editor - {creatureName} ({deckName})";
        Width = 700;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;
        Background = EditorConstants.DEFAULT_WINDOW_BACKGROUND;
        
        // Initialize UI controls
        _spellListBox = new ListBox
        {
            ItemsSource = _spellTemplateIds,
            Height = 300,
            Background = new SolidColorBrush(Color.FromArgb(255, 44, 44, 44)), // #2C2C2C
            Foreground = Brushes.White,
            SelectionMode = SelectionMode.Single
        };

        _addSpellTextBox = new TextBox
        {
            Watermark = "Enter spell template ID (e.g., 2062265892)",
            Height = 30,
            FontSize = 12
        };

        _addSpellButton = new Button
        {
            Content = "Add Spell",
            Height = 30,
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Avalonia.Thickness(0, 5, 0, 0)
        };

        _removeSpellButton = new Button
        {
            Content = "Remove Selected",
            Height = 30,
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Avalonia.Thickness(110, 5, 0, 0),
            IsEnabled = false
        };

        _saveButton = new Button
        {
            Content = "Save to Data Pack",
            Height = 35,
            Width = 140,
            HorizontalAlignment = HorizontalAlignment.Left,
            Classes = { "accent" }
        };

        _cancelButton = new Button
        {
            Content = "Cancel",
            Height = 35,
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Avalonia.Thickness(150, 0, 0, 0)
        };

        // Wire up events
        _addSpellButton.Click += AddSpell_Click;
        _removeSpellButton.Click += RemoveSpell_Click;
        _saveButton.Click += Save_Click;
        _cancelButton.Click += Cancel_Click;
        _spellListBox.SelectionChanged += SpellList_SelectionChanged;
        _addSpellTextBox.KeyDown += AddSpellTextBox_KeyDown;
        
        // Handle window closing to check for unsaved changes
        Closing += async (sender, e) =>
        {
            if (_isDirty && !WasSaved)
            {
                e.Cancel = true;
                
                var result = await ShowConfirmationDialog(
                    "Unsaved Changes",
                    "You have unsaved changes. Are you sure you want to close without saving?");
                
                if (result)
                {
                    _isDirty = false; // Prevent further dialogs
                    Close();
                }
            }
        };

        InitializeComponent();
        
        // Load existing deck data if provided (after UI is initialized)
        if (existingDeck != null)
        {
            Console.WriteLine($"[DECK DEBUG] Loading existing deck data: {existingDeck.SpellTemplateIds.Count} spells");
            LoadDeckData(existingDeck);
        }
        else
        {
            Console.WriteLine($"[DECK DEBUG] No existing deck data provided - creating new deck");
        }
    }

    private void InitializeComponent()
    {
        var mainPanel = new StackPanel
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Margin = EditorConstants.DEFAULT_MARGIN_THICKNESS
        };

        // Database authority notice (same style as NPC inventory editor)
        var authorityNotice = new Border
        {
            Background = Brushes.DarkBlue,
            BorderBrush = Brushes.LightBlue,
            BorderThickness = new Avalonia.Thickness(2),
            CornerRadius = EditorConstants.DEFAULT_CORNER_RADIUS,
            Padding = new Avalonia.Thickness(10),
            Margin = new Avalonia.Thickness(0, 0, 0, 10),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "DATA PACK EDITING AUTHORITY",
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.Yellow,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "You are authorized to edit data pack creature spell deck data.",
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Avalonia.Thickness(0, 4, 0, 0)
                    },
                    new TextBlock
                    {
                        Text = $"Editing: {_creatureName} (Deck: {_deckName})",
                        Foreground = Brushes.LightBlue,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontStyle = FontStyle.Italic,
                        Margin = new Avalonia.Thickness(0, 2, 0, 0)
                    }
                }
            }
        };

        // Spell deck section
        var spellSection = CreateGroupBox("Spell Template IDs", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = $"Current deck contains {_spellTemplateIds.Count} spell(s).",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyle.Italic
                },
                _spellListBox,
                new TextBlock
                {
                    Text = "Add new spell:",
                    FontSize = 12,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    Margin = new Avalonia.Thickness(0, 10, 0, 5)
                },
                _addSpellTextBox,
                new Grid
                {
                    Children = { _addSpellButton, _removeSpellButton }
                }
            }
        });

        // Action buttons
        var actionButtons = new Grid
        {
            Margin = new Avalonia.Thickness(0, 20, 0, 0),
            Children = { _saveButton, _cancelButton }
        };

        mainPanel.Children.Add(authorityNotice);
        mainPanel.Children.Add(spellSection);
        mainPanel.Children.Add(actionButtons);

        Content = new ScrollViewer { Content = mainPanel };
    }

    private Border CreateGroupBox(string title, Control content)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 34, 34, 34)), // #222222
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 85, 85, 85)), // #555555
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = EditorConstants.DEFAULT_CORNER_RADIUS,
            Padding = new Avalonia.Thickness(15),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 14,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White,
                        Margin = new Avalonia.Thickness(0, 0, 0, 10)
                    },
                    content
                }
            }
        };
    }

    private void LoadDeckData(CreatureDeck deck)
    {
        Console.WriteLine($"[DECK DEBUG] LoadDeckData called - clearing {_spellTemplateIds.Count} existing spells");
        _spellTemplateIds.Clear();
        
        Console.WriteLine($"[DECK DEBUG] Loading {deck.SpellTemplateIds.Count} spells from deck '{deck.DeckName}'");
        foreach (var spellId in deck.SpellTemplateIds)
        {
            Console.WriteLine($"[DECK DEBUG] Adding spell ID: {spellId}");
            _spellTemplateIds.Add(spellId);
        }
        
        Console.WriteLine($"[DECK DEBUG] LoadDeckData completed - Spells collection now has {_spellTemplateIds.Count} items");
    }

    private void AddSpellTextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            AddSpell();
        }
    }

    private void AddSpell_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddSpell();
    }

    private void AddSpell()
    {
        if (string.IsNullOrWhiteSpace(_addSpellTextBox.Text))
        {
            MessageService.Error("Please enter a valid spell template ID.").Send();
            return;
        }

        if (!uint.TryParse(_addSpellTextBox.Text.Trim(), out var spellId))
        {
            MessageService.Error("Invalid spell template ID. Please enter a numeric value.").Send();
            return;
        }

        if (_spellTemplateIds.Contains(spellId))
        {
            MessageService.Info("This spell is already in the deck.").Send();
            return;
        }

        _spellTemplateIds.Add(spellId);
        _addSpellTextBox.Text = string.Empty;
        _isDirty = true;
        
        MessageService.Info($"Added spell {spellId} to deck.").Send();
    }

    private void RemoveSpell_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_spellListBox.SelectedItem is uint selectedSpell)
        {
            _spellTemplateIds.Remove(selectedSpell);
            _isDirty = true;
            MessageService.Info($"Removed spell {selectedSpell} from deck.").Send();
        }
    }

    private void SpellList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _removeSpellButton.IsEnabled = _spellListBox.SelectedItem != null;
    }

    private async void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (!await DataPack.IsLoadedAsync())
            {
                MessageService.Error("Data pack not available. Please ensure a data pack folder is configured and accessible.").Send();
                return;
            }

            // Create the deck to save
            var deck = new CreatureDeck
            {
                DeckName = _deckName,
                SpellTemplateIds = _spellTemplateIds.ToList()
            };

            // Show saving progress
            MessageService.Info("Saving creature deck to data pack...").Send();

            // Save to database
            var success = await CreatureDeckService.SaveCreatureDeckAsync(deck);

            if (success)
            {
                WasSaved = true;
                MessageService.Info($"Creature deck saved successfully to data pack.\n\nCreature: {_creatureName}\nDeck: {_deckName}\nSpells: {_spellTemplateIds.Count}").Send();
                Close();
            }
            else
            {
                MessageService.Error("Failed to save creature deck to data pack.").Send();
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Failed to save creature deck to data pack: {ex.Message}").Send();
        }
    }

    private async void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isDirty)
        {
            var result = await ShowConfirmationDialog(
                "Unsaved Changes",
                "You have unsaved changes. Are you sure you want to close without saving?");
            
            if (result)
            {
                Close();
            }
        }
        else
        {
            Close();
        }
    }
    
    private async Task<bool> ShowConfirmationDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = EditorConstants.DEFAULT_WINDOW_BACKGROUND
        };

        var stackPanel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20)
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 0, 0, 20),
            Foreground = Brushes.White
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var yesButton = new Button
        {
            Content = "Yes",
            Margin = new Avalonia.Thickness(0, 0, 10, 0),
            Padding = new Avalonia.Thickness(20, 8),
            Background = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38)), // Red
            Foreground = Brushes.White
        };

        var noButton = new Button
        {
            Content = "No",
            Padding = new Avalonia.Thickness(20, 8),
            Background = new SolidColorBrush(Color.FromArgb(255, 74, 74, 74)), // Dark gray
            Foreground = Brushes.White
        };

        bool result = false;

        yesButton.Click += (s, e) =>
        {
            result = true;
            dialog.Close();
        };

        noButton.Click += (s, e) =>
        {
            result = false;
            dialog.Close();
        };

        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);
        stackPanel.Children.Add(messageText);
        stackPanel.Children.Add(buttonPanel);
        dialog.Content = stackPanel;

        await dialog.ShowDialog(this);
        return result;
    }
}