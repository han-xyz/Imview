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
using Imview.Core.Common.Constants;
using Imview.Core.Controls.Base;
using Imview.Core.Models;
using Imview.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Imview.Core.Data;

namespace Imview.Core.Controls;

/// <summary>
/// Editor for managing NPC inventory with database authority
/// </summary>
public class NpcInventoryEditor : EditorWindowBase<NpcInventory>
{
    private readonly NpcInventory _npcInventory;
    private readonly ulong _templateId;
    private readonly string _npcName;
    private readonly ObservableCollection<ulong> _inventoryItems;
    private readonly ListBox _inventoryListBox;
    private readonly TextBox _addItemTextBox;
    private readonly Button _addItemButton;
    private readonly Button _removeItemButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private bool _isDirty = false;

    /// <summary>
    /// Gets whether the data was successfully saved to the database
    /// </summary>
    public bool WasSaved { get; private set; } = false;
    
    /// <summary>
    /// Gets whether there are unsaved changes
    /// </summary>
    public bool IsDirty => _isDirty;

    public NpcInventoryEditor(ulong templateId, string npcName, NpcInventory? existingInventory = null)
        : base($"Edit NPC Inventory - {npcName} (ID: {templateId})")
    {
        _templateId = templateId;
        _npcName = npcName;
        _npcInventory = existingInventory ?? new NpcInventory { TemplateID = templateId };
        _inventoryItems = new ObservableCollection<ulong>(_npcInventory.Inventory);

        Width = EditorConstants.DEFAULT_WINDOW_WIDTH + 100;
        Height = 600;
        Background = EditorConstants.DEFAULT_WINDOW_BACKGROUND;

        // Initialize controls
        _inventoryListBox = new ListBox
        {
            ItemsSource = _inventoryItems,
            Height = 300,
            Background = new SolidColorBrush(Color.FromArgb(255, 44, 44, 44)), // #2C2C2C
            Foreground = Brushes.White,
            SelectionMode = SelectionMode.Single
        };

        _addItemTextBox = new TextBox
        {
            Watermark = "Enter item template ID (e.g., 136602)",
            Height = 30,
            FontSize = 12
        };

        _addItemButton = new Button
        {
            Content = "Add Item",
            Height = 30,
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 5, 0, 0)
        };

        _removeItemButton = new Button
        {
            Content = "Remove Selected",
            Height = 30,
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(110, 5, 0, 0),
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
            Margin = new Thickness(150, 0, 0, 0)
        };

        // Wire up events
        _addItemButton.Click += AddItem_Click;
        _removeItemButton.Click += RemoveItem_Click;
        _saveButton.Click += Save_Click;
        _cancelButton.Click += Cancel_Click;
        _inventoryListBox.SelectionChanged += InventoryList_SelectionChanged;
        _addItemTextBox.KeyDown += AddItemTextBox_KeyDown;
        
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
    }

    private void InitializeComponent()
    {
        var mainPanel = new StackPanel
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Margin = EditorConstants.DEFAULT_MARGIN_THICKNESS
        };

        // Database authority notice (same style as teleport editor)
        var authorityNotice = new Border
        {
            Background = Brushes.DarkBlue,
            BorderBrush = Brushes.LightBlue,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10),
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
                        Text = "You are authorized to edit data pack NPC inventory data.",
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 4, 0, 0)
                    },
                    new TextBlock
                    {
                        Text = $"Editing: {_npcName} (Template ID: {_templateId})",
                        Foreground = Brushes.LightBlue,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontStyle = FontStyle.Italic,
                        Margin = new Thickness(0, 2, 0, 0)
                    }
                }
            }
        };

        // Inventory section
        var inventorySection = CreateGroupBox("Inventory Items", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = $"Current inventory contains {_inventoryItems.Count} item(s).",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyle.Italic
                },
                _inventoryListBox,
                new TextBlock
                {
                    Text = "Add new item:",
                    FontSize = 12,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 10, 0, 5)
                },
                _addItemTextBox,
                new Grid
                {
                    Children = { _addItemButton, _removeItemButton }
                }
            }
        });

        // Action buttons
        var actionButtons = new Grid
        {
            Margin = new Thickness(0, 20, 0, 0),
            Children = { _saveButton, _cancelButton }
        };

        mainPanel.Children.Add(authorityNotice);
        mainPanel.Children.Add(inventorySection);
        mainPanel.Children.Add(actionButtons);

        Content = new ScrollViewer { Content = mainPanel };
    }

    private void AddItemTextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            AddItem();
        }
    }

    private void AddItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddItem();
    }

    private void AddItem()
    {
        if (string.IsNullOrWhiteSpace(_addItemTextBox.Text))
        {
            MessageService.Error("Please enter a valid item template ID.").Send();
            return;
        }

        if (!ulong.TryParse(_addItemTextBox.Text.Trim(), out var itemId))
        {
            MessageService.Error("Invalid item template ID. Please enter a numeric value.").Send();
            return;
        }

        if (_inventoryItems.Contains(itemId))
        {
            MessageService.Info("This item is already in the inventory.").Send();
            return;
        }

        _inventoryItems.Add(itemId);
        _addItemTextBox.Text = string.Empty;
        _isDirty = true;
        
        MessageService.Info($"Added item {itemId} to inventory.").Send();
    }

    private void RemoveItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_inventoryListBox.SelectedItem is ulong selectedItem)
        {
            _inventoryItems.Remove(selectedItem);
            _isDirty = true;
            MessageService.Info($"Removed item {selectedItem} from inventory.").Send();
        }
    }

    private void InventoryList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _removeItemButton.IsEnabled = _inventoryListBox.SelectedItem != null;
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

            // Update the inventory
            _npcInventory.Inventory = _inventoryItems.ToList();

            // Show saving progress
            MessageService.Info("Saving NPC inventory to data pack...").Send();

            // Save to database
            var success = await NpcInventoryService.SaveNpcInventoryAsync(_npcInventory);

            if (success)
            {
                WasSaved = true;
                MessageService.Info($"NPC inventory saved successfully to data pack.\n\nNPC: {_npcName}\nTemplate ID: {_templateId}\nItems: {_inventoryItems.Count}").Send();
                Close();
            }
            else
            {
                MessageService.Error("Failed to save NPC inventory to data pack.").Send();
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Failed to save NPC inventory to data pack: {ex.Message}").Send();
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
            Margin = new Thickness(20)
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20),
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
            Margin = new Thickness(0, 0, 10, 0),
            Padding = new Thickness(20, 8),
            Background = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38)), // Red
            Foreground = Brushes.White
        };

        var noButton = new Button
        {
            Content = "No",
            Padding = new Thickness(20, 8),
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
