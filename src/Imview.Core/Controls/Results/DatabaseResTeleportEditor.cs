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
using Imview.Core.Data;
using Imview.Core.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Imview.Core.Common.Extensions;

namespace Imview.Core.Controls.Results;

/// <summary>
/// Database-enabled ResTeleport editor that saves changes to the world database
/// </summary>
public class DatabaseResTeleportEditor : EditorWindowBase<ResTeleport>
{
    private readonly ResTeleport _result;
    private readonly string _zoneName;
    private readonly string _triggerName;
    private readonly ZoneDataService _zoneDataService;
    private readonly TextBox _destinationLocInput;
    private readonly ComboBox _destinationZoneComboBox;
    private readonly ComboBox _destinationLocationComboBox;
    private readonly NumericUpDown _exitTeleporterInput;
    private readonly NumericUpDown _teleporterTagInput;
    private readonly ComboBox _teleportTypeComboBox;
    private readonly NumericUpDown _transitionIdInput;
    private readonly bool _isNew;
    private List<string> _availableZones = [];

    /// <summary>
    /// Gets whether the data was successfully saved to the database
    /// </summary>
    public bool WasSaved { get; private set; } = false;

    public DatabaseResTeleportEditor(ResTeleport result, string zoneName, string triggerName, ZoneDataService? zoneDataService = null, bool isNew = false)
        : base(isNew ? $"Create Database Teleport - {triggerName} ({zoneName})" : $"Edit Database Teleport - {triggerName} ({zoneName})")
    {
        _result = result ?? throw new ArgumentNullException(nameof(result));
        _zoneName = zoneName ?? throw new ArgumentNullException(nameof(zoneName));
        _triggerName = triggerName ?? throw new ArgumentNullException(nameof(triggerName));
        _zoneDataService = zoneDataService ?? new ZoneDataService();
        _isNew = isNew;
        
        Width = EditorConstants.DEFAULT_WINDOW_WIDTH + 100; // Slightly wider for database info
        Height = 600; // Taller for additional info
        Background = EditorConstants.DEFAULT_WINDOW_BACKGROUND;
        
        _destinationLocInput = new TextBox 
        {
            Text = _result.m_destinationLoc ?? "",
            Watermark = "Destination coordinates (x,y,z,yaw)",
            IsReadOnly = false // Allow manual editing, but will auto-populate from location selection
        };
        
        _destinationZoneComboBox = new ComboBox 
        {
            ItemsSource = new[] { "Loading zones..." },
            SelectedIndex = 0,
            IsEnabled = false // Will be enabled after zones load
        };
        
        _destinationLocationComboBox = new ComboBox 
        {
            ItemsSource = new[] { "Select zone first" },
            SelectedIndex = 0,
            IsEnabled = false // Will be enabled after zone is selected
        };
        
        _exitTeleporterInput = new NumericUpDown 
        {
            Value = _result.m_exitTeleporter,
            Minimum = 0,
            Maximum = 255,
            Increment = 1
        };
        
        _teleporterTagInput = new NumericUpDown 
        {
            Value = _result.m_teleporterTag,
            Minimum = 0,
            Maximum = 255,
            Increment = 1
        };
        
        _teleportTypeComboBox = new ComboBox 
        {
            ItemsSource = new[] { "TELEPORT_STATIC" },
            SelectedIndex = 0
        };
        
        _transitionIdInput = new NumericUpDown 
        {
            Value = _result.m_transitionID,
            Minimum = 0,
            Maximum = 255,
            Increment = 1
        };
        
        InitializeComponent();
        
        // Load zones asynchronously
        _ = LoadAvailableZonesAsync();
    }
    
    private void InitializeComponent() 
    {
        // Database authority notice
        var authorityNotice = new Border
        {
            Background = Avalonia.Media.Brushes.DarkBlue,
            BorderBrush = Avalonia.Media.Brushes.LightBlue,
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
                        Text = "DATABASE EDITING AUTHORITY", 
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = Avalonia.Media.Brushes.Yellow,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock 
                    { 
                        Text = "You are authorized to edit world database teleport data.",
                        Foreground = Avalonia.Media.Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 4, 0, 0)
                    },
                    new TextBlock 
                    { 
                        Text = $"Editing: {_triggerName} in {_zoneName}",
                        Foreground = Avalonia.Media.Brushes.LightBlue,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontStyle = Avalonia.Media.FontStyle.Italic,
                        Margin = new Thickness(0, 2, 0, 0)
                    }
                }
            }
        };
        
        var zonePanel = new StackPanel 
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = 
            {
                new TextBlock { Text = "Destination Zone:" },
                _destinationZoneComboBox,
                new TextBlock 
                { 
                    Text = "Select the destination zone from the available zones", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var locationPanel = new StackPanel 
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = 
            {
                new TextBlock { Text = "Destination Location:" },
                _destinationLocationComboBox,
                new TextBlock 
                { 
                    Text = "Select a named location in the destination zone", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var coordinatesPanel = new StackPanel 
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = 
            {
                new TextBlock { Text = "Coordinates:" },
                _destinationLocInput,
                new TextBlock 
                { 
                    Text = "Coordinates are auto-filled from selected location, or can be manually entered (format: x,y,z,yaw). Manual entry is required for zones where location data is unavailable.", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var teleporterPanel = new StackPanel 
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = 
            {
                new TextBlock { Text = "Exit Teleporter:" },
                _exitTeleporterInput,
                new TextBlock 
                { 
                    Text = "Exit teleporter identifier (0-255)", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var tagPanel = new StackPanel 
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = 
            {
                new TextBlock { Text = "Teleporter Tag:" },
                _teleporterTagInput,
                new TextBlock 
                { 
                    Text = "Teleporter tag identifier (0-255)", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var typePanel = new StackPanel 
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = 
            {
                new TextBlock { Text = "Teleport Type:" },
                _teleportTypeComboBox,
                new TextBlock 
                { 
                    Text = "Type of teleport operation to perform", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        var transitionPanel = new StackPanel 
        {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = 
            {
                new TextBlock { Text = "Transition ID:" },
                _transitionIdInput,
                new TextBlock 
                { 
                    Text = "Transition identifier for the teleport effect (0-255)", 
                    FontSize = 12,
                    FontStyle = Avalonia.Media.FontStyle.Italic,
                    Foreground = Avalonia.Media.Brushes.Gray
                }
            }
        };
        
        // Add everything to the main panel
        MainPanel.Children.Add(authorityNotice);
        MainPanel.Children.Add(CreateGroupBox("Destination Zone", zonePanel));
        MainPanel.Children.Add(CreateGroupBox("Destination Location", locationPanel));
        MainPanel.Children.Add(CreateGroupBox("Destination Coordinates", coordinatesPanel));
        MainPanel.Children.Add(CreateGroupBox("Teleporter Configuration", teleporterPanel));
        MainPanel.Children.Add(CreateGroupBox("Teleporter Tag", tagPanel));
        MainPanel.Children.Add(CreateGroupBox("Teleport Type", typePanel));
        MainPanel.Children.Add(CreateGroupBox("Transition Settings", transitionPanel));
        MainPanel.Children.Add(CreateActionButtons(SaveToDatabase, Cancel));
    }
    
    /// <summary>
    /// Load available zones from ZoneDataService
    /// </summary>
    private async Task LoadAvailableZonesAsync()
    {
        try
        {
            var zones = await _zoneDataService.GetAvailableZonesAsync();
            
            // Update the ComboBox on the UI thread
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
            {
                _availableZones = zones;
                _destinationZoneComboBox.ItemsSource = zones;
                _destinationZoneComboBox.IsEnabled = true;
                
                // Select current destination zone if it exists in the list
                if (!string.IsNullOrEmpty(_result.m_destinationZone) && zones.Contains(_result.m_destinationZone))
                {
                    _destinationZoneComboBox.SelectedItem = _result.m_destinationZone;
                }
                
                // Wire up the selection changed event
                _destinationZoneComboBox.SelectionChanged += OnDestinationZoneChanged;
                
                // If a zone is already selected, load its locations
                if (_destinationZoneComboBox.SelectedItem is string selectedZone)
                {
                    _ = LoadZoneLocationsAsync(selectedZone, true); // Pass true for initial load to trigger coordinate lookup
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load zones: {ex.Message}");
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
            {
                _destinationZoneComboBox.ItemsSource = new[] { "Error loading zones" };
                _destinationZoneComboBox.SelectedIndex = 0;
            });
        }
    }
    
    /// <summary>
    /// Handle destination zone selection change
    /// </summary>
    private async void OnDestinationZoneChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_destinationZoneComboBox.SelectedItem is string selectedZone)
        {
            await LoadZoneLocationsAsync(selectedZone, false); // Don't do coordinate lookup for user-initiated zone changes
        }
    }
    
    /// <summary>
    /// Load locations for the specified zone
    /// </summary>
    /// <param name="zoneName">The zone to load locations for</param>
    /// <param name="tryCoordinateLookup">Whether to try finding a location that matches current coordinates</param>
    private async Task LoadZoneLocationsAsync(string zoneName, bool tryCoordinateLookup = false)
    {
        try
        {
            // Reset location selection
            _destinationLocationComboBox.ItemsSource = new[] { "Loading locations..." };
            _destinationLocationComboBox.SelectedIndex = 0;
            _destinationLocationComboBox.IsEnabled = false;
            // Don't clear coordinates - keep existing values for coordinate-to-location lookup
            
            var (zoneData, _, _, _, _, _, _, _) = await _zoneDataService.LoadZoneDataAsync(zoneName);
            
            if (zoneData?.m_locationList != null && zoneData.m_locationList.Count > 0)
            {
                var locationNames = zoneData.m_locationList.Select(loc => loc.m_locName.OrIfEmpty("Unnamed")).ToList();
                
                // Update the location ComboBox on the UI thread
                Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                {
                    _destinationLocationComboBox.ItemsSource = locationNames;
                    _destinationLocationComboBox.IsEnabled = true;
                    
                    // Wire up the selection changed event
                    _destinationLocationComboBox.SelectionChanged -= OnDestinationLocationChanged; // Remove existing
                    _destinationLocationComboBox.SelectionChanged += OnDestinationLocationChanged;
                    
                    // Try to find matching location by coordinates if requested
                    if (tryCoordinateLookup)
                    {
                        Console.WriteLine($"Trying coordinate lookup. Coordinates: '{_destinationLocInput.Text}'");
                        if (!string.IsNullOrEmpty(_destinationLocInput.Text))
                        {
                            var matchingLocation = FindLocationByCoordinates(zoneData, _destinationLocInput.Text);
                            if (matchingLocation != null)
                            {
                                var matchingName = matchingLocation.m_locName?.ToString();
                                Console.WriteLine($"Found matching location: '{matchingName}'");
                                if (!string.IsNullOrEmpty(matchingName) && locationNames.Contains(matchingName))
                                {
                                    _destinationLocationComboBox.SelectedItem = matchingName;
                                    Console.WriteLine($"Pre-selected location '{matchingName}' based on coordinates: {_destinationLocInput.Text}");
                                }
                                else
                                {
                                    Console.WriteLine($"Location '{matchingName}' not found in location names list");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"No matching location found for coordinates: {_destinationLocInput.Text}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("No coordinates to lookup");
                        }
                    }
                });
                
                Console.WriteLine($"Loaded {locationNames.Count} locations for zone '{zoneName}'");
            }
            else
            {
                Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                {
                    _destinationLocationComboBox.ItemsSource = new[] { "No locations available" };
                    _destinationLocationComboBox.SelectedIndex = 0;
                    _destinationLocationComboBox.IsEnabled = false;
                });
                
                Console.WriteLine($"No locations found for zone '{zoneName}'");
            }
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"Zone WAD file not found for zone '{zoneName}': {ex.Message}");
            var errorMessage = "Zone not available for download";
            
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
            {
                _destinationLocationComboBox.ItemsSource = new[] { errorMessage };
                _destinationLocationComboBox.SelectedIndex = 0;
                _destinationLocationComboBox.IsEnabled = false;
                
                // Ensure coordinates field is editable when zone data is unavailable
                _destinationLocInput.IsReadOnly = false;
                _destinationLocInput.Watermark = "Enter coordinates manually (format: x,y,z,yaw)";
                
                // Show a brief notification about the issue
                MessageService.Info($"Zone '{zoneName}' is not available for download. You can still enter coordinates manually.").Send();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load locations for zone '{zoneName}': {ex.Message}");
            Console.WriteLine($"Exception stack trace: {ex.StackTrace}");
            
            string errorMessage;
            if (ex.Message.Contains("Failed to download"))
            {
                errorMessage = "Download failed";
            }
            else
            {
                errorMessage = "Error loading locations";
            }
            
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
            {
                _destinationLocationComboBox.ItemsSource = new[] { errorMessage };
                _destinationLocationComboBox.SelectedIndex = 0;
                _destinationLocationComboBox.IsEnabled = false;
                
                // Ensure coordinates field is editable when zone data is unavailable
                _destinationLocInput.IsReadOnly = false;
                _destinationLocInput.Watermark = "Enter coordinates manually (format: x,y,z,yaw)";
                
                // Show a brief notification about the issue
                if (errorMessage.Contains("not available"))
                {
                    MessageService.Info($"Zone '{zoneName}' is not available for download. You can still enter coordinates manually.").Send();
                }
                else if (errorMessage.Contains("Download failed"))
                {
                    MessageService.Error($"Failed to download zone data for '{zoneName}'. You can still enter coordinates manually.").Send();
                }
            });
        }
    }
    
    /// <summary>
    /// Finds a location in the zone data that matches the given coordinates string
    /// </summary>
    /// <param name="zoneData">The zone data to search in</param>
    /// <param name="coordinatesText">Coordinates in format "x,y,z,direction" or "x,y,z"</param>
    /// <returns>The matching location or null if not found</returns>
    private dynamic? FindLocationByCoordinates(WizZoneData? zoneData, string coordinatesText)
    {
        if (zoneData?.m_locationList == null || string.IsNullOrWhiteSpace(coordinatesText))
            return null;
            
        try
        {
            Console.WriteLine($"Parsing coordinates: '{coordinatesText}'");
            var parts = coordinatesText.Split(',');
            if (parts.Length < 3) 
            {
                Console.WriteLine($"Not enough coordinate parts: {parts.Length}");
                return null;
            }
                
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                Console.WriteLine($"Failed to parse coordinates: x={parts[0]}, y={parts[1]}, z={parts[2]}");
                return null;
            }
                
            // Optional direction component
            float direction = 0f;
            if (parts.Length >= 4)
            {
                float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out direction);
            }
            
            Console.WriteLine($"Looking for coordinates: x={x}, y={y}, z={z}, direction={direction} (tolerance=0.1)");
            Console.WriteLine($"Zone has {zoneData.m_locationList.Count} locations");
            
            // Find location that matches these coordinates (with some tolerance for floating point comparison)
            const float tolerance = 0.1f;
            
            foreach (var loc in zoneData.m_locationList.Where(l => l != null))
            {
                var locName = loc.m_locName.OrIfEmpty("Unnamed");
                var xDiff = Math.Abs(loc.m_location.X - x);
                var yDiff = Math.Abs(loc.m_location.Y - y);
                var zDiff = Math.Abs(loc.m_location.Z - z);
                var dirDiff = Math.Abs(loc.m_direction - direction);
                
                Console.WriteLine($"Location '{locName}': pos=({loc.m_location.X}, {loc.m_location.Y}, {loc.m_location.Z}), dir={loc.m_direction}, diffs=({xDiff:F3}, {yDiff:F3}, {zDiff:F3}, {dirDiff:F3})");
                
                if (xDiff < tolerance && yDiff < tolerance && zDiff < tolerance && 
                    (parts.Length < 4 || dirDiff < tolerance))
                {
                    Console.WriteLine($"Found matching location: '{locName}'");
                    return loc;
                }
            }
            
            Console.WriteLine("No matching location found");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing coordinates '{coordinatesText}': {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Handle destination location selection change
    /// </summary>
    private async void OnDestinationLocationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_destinationLocationComboBox.SelectedItem is string selectedLocationName &&
            _destinationZoneComboBox.SelectedItem is string selectedZone)
        {
            try
            {
                var (zoneData, _, _, _, _, _, _, _) = await _zoneDataService.LoadZoneDataAsync(selectedZone);
                
                var location = zoneData?.m_locationList?.FirstOrDefault(loc => 
                    loc.m_locName.ToString() == selectedLocationName);
                
                if (location != null)
                {
                    var coordinates = ZoneTransferService.FormatDestinationLoc(location.m_location.X, location.m_location.Y, location.m_location.Z, location.m_direction);
                    
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                    {
                        _destinationLocInput.Text = coordinates;
                    });
                    
                    Console.WriteLine($"Set coordinates for location '{selectedLocationName}': {coordinates}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get coordinates for location '{selectedLocationName}': {ex.Message}");
            }
        }
    }
    
    private async void SaveToDatabase()
    {
        try 
        {
            // Validate input
            if (_destinationZoneComboBox.SelectedItem is not string destinationZone || !_availableZones.Contains(destinationZone))
            {
                MessageService.Error("Please select a destination zone.").Send();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(_destinationLocInput.Text))
            {
                MessageService.Error("Please enter destination coordinates or select a location.").Send();
                return;
            }

            if (ParseDestinationLoc(_destinationLocInput.Text) is not { } coordinates)
            {
                MessageService.Error("Destination coordinates must be x,y,z or x,y,z,yaw, with '.' as the decimal separator.").Send();
                return;
            }
            
            // Check if the location ComboBox shows an error state
            var locationSource = _destinationLocationComboBox.ItemsSource as string[];
            if (locationSource != null && locationSource.Length > 0 && 
                (locationSource[0].Contains("Error") || locationSource[0].Contains("not available") || locationSource[0].Contains("Download failed")))
            {
                MessageService.Info($"Zone '{destinationZone}' data could not be loaded, but coordinates can still be saved manually.\n\nNote: Location validation is not available for this zone.").Send();
            }
            
            var edited = WorldRecordCopy.Detached(_result);
            edited.m_destinationLoc = ZoneTransferService.FormatDestinationLoc(coordinates);
            edited.m_destinationZone = destinationZone;
            edited.m_exitTeleporter = (byte)(_exitTeleporterInput.Value ?? 0);
            edited.m_teleporterTag = (byte)(_teleporterTagInput.Value ?? 0);
            edited.m_transitionID = (byte)(_transitionIdInput.Value ?? 0);
            
            MessageService.Info("Saving teleport data to the data pack...").Send();
            
            await SaveTeleportToDataPack(edited);

            _result.m_destinationLoc = edited.m_destinationLoc;
            _result.m_destinationZone = edited.m_destinationZone;
            _result.m_exitTeleporter = edited.m_exitTeleporter;
            _result.m_teleporterTag = edited.m_teleporterTag;
            _result.m_transitionID = edited.m_transitionID;
            WasSaved = true;
            
            MessageService.Info($"Teleport data saved successfully to the data pack.\n\nZone: {_zoneName}\nTrigger: {_triggerName}\nDestination: {_result.m_destinationZone}").Send();
            
            Close();
        } 
        catch (Exception ex) 
        {
            MessageService.Error($"Failed to save teleport to the data pack: {ex.Message}").Send();
        }
    }
    
    private static float[]? ParseDestinationLoc(string text)
    {
        var parts = text.Split(',');
        if (parts.Length is not (3 or 4))
            return null;

        var coordinates = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out coordinates[i]))
                return null;
        }
        return coordinates;
    }

    private async Task SaveTeleportToDataPack(ResTeleport teleport)
    {
        var zoneData = await ZoneTransferService.GetZoneTransferAsync(_zoneName)
            ?? new WizardZoneData { ZoneName = _zoneName };

        var index = zoneData.Teleports.FindIndex(t => t.TriggerName == _triggerName);
        var entry = new WizardTeleportData { TriggerName = _triggerName, Teleport = teleport };
        if (index >= 0)
        {
            zoneData.Teleports[index] = entry;
        }
        else
        {
            zoneData.Teleports.Add(entry);
        }

        await ZoneTransferService.SaveZoneTransferAsync(zoneData);
    }
}
