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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;
using Avalonia.Controls.Shapes;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Layout;
using Imview.Core.Services;
using Imview.Core.Models;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.ObjectProperty;
using Imcodec.Math;
using Imcodec.BCD;
using BcdGeomParams = Imcodec.BCD.GeomParams;
using WizardTea.Core;
using Imview.PacketReader.Services;
using Imview.Core.Data;
using Imview.Core.Database;
using Imview.Core.Database.Services;
using Imview.Core.Controls;
using Imcodec.IO;
using Imview.Core.Common.Extensions;

namespace Imview.Core.ViewModels;

public class ZoneEditorViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;
    private readonly ZoneDataService _zoneDataService;
    private string _selectedZone = string.Empty;
    private ZoneVisualizationObject? _selectedObject = null;
    private CoreObjectInfo? _selectedCoreObject = null;
    private PropertyClass? _selectedTemplate = null;
    private CollisionVisualizationObject? _selectedCollision = null;
    private PathVisualizationObject? _selectedPath = null;
    private NifMeshVisualizationObject? _selectedMesh = null;
    private VolumeVisualizationObject? _selectedVolume = null;
    private TriggerVisualizationObject? _selectedTrigger = null;
    private SpawnVisualizationObject? _selectedSpawn = null;
    private PropertyClass? _selectedSpawnTemplate = null;

    // Tracks selection in the Paths & Spawns tree so we can clear it when selecting elsewhere
    private object? _selectedHierarchyItem = null;
    private WizZoneData? _currentZoneData = null;
    private Bcd? _currentCollisionData = null;
    private NifFile? _currentSceneFile = null;
    private SpawnManager? _currentSpawnData = null;
    private PathTemplateList? _currentPathData = null;
    private NodeTemplateList? _currentNodeData = null;
    private WizZoneVolumes? _currentVolumeData = null;
    private WizZoneTriggers? _currentTriggerData = null;
    private NifGeometryProcessor? _nifProcessor = null;
    private bool _isLoading = false;

    // Track whether we've already auto-prompted for zone selection for this tab instance
    private bool _autoPromptedZoneSelection = false;

    // World database availability
    private bool _isWorldDatabaseReady = false;
    private string _worldDatabaseStatusText = string.Empty;
    private bool _showCollisions = true;
    private bool _showZoneObjects = true;
    private bool _showNifGeometry = true;
    private bool _showSpawns = true;
    private bool _showPaths = true;
    private bool _showNodes = true; // Make visible by default for debugging
    private bool _showVolumes = true; // Make visible by default
    private bool _showTriggers = true; // Make visible by default
    
    // Collision shape filters - default to true so all shapes show initially
    private bool _showBoxCollisions = true;
    private bool _showSphereCollisions = true;
    private bool _showCylinderCollisions = true;
    private bool _showTubeCollisions = true;
    private bool _showPlaneCollisions = true;
    private bool _showMeshCollisions = false;
    private bool _showRayCollisions = true;
    
private Canvas? _zoneObjectCanvas = null;
    private ScrollViewer? _scrollViewer = null;
    private Canvas? _overlayCanvas = null;
    
    // Highlight styling
    private static readonly Avalonia.Media.Color HighlightColor = Avalonia.Media.Color.FromRgb(255, 215, 0); // Imview gold (#FFD700)
    private static readonly SolidColorBrush HighlightBrush = new SolidColorBrush(HighlightColor);
    private const double HighlightThickness = 4.0;

    // Base path styling (uniform color for all paths)
    private static readonly Avalonia.Media.Color PathBaseColor = Avalonia.Media.Color.FromRgb(102, 102, 102); // Dark gray (#666666) for unselected paths
    private static readonly SolidColorBrush PathBaseBrush = new SolidColorBrush(PathBaseColor);

    private Control? _triggerHighlightMarker;
    
    // Viewport state for camera control
    private double _zoomLevel = 0.5; // Default to 50% zoom
    private double _panX = 0.0;
    private double _panY = 0.0;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 5.0;
    private const double ZoomStep = 0.1;
    private const double PanStep = 50.0;
    private const double CanvasCenterX = 10000.0;
    private const double CanvasCenterY = 10000.0;

    public ZoneEditorViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _zoneDataService = new ZoneDataService();
        
        LoadZoneCommand = ReactiveCommand.Create(LoadZone);
        SaveZoneCommand = ReactiveCommand.Create(SaveZone);
        SelectObjectCommand = ReactiveCommand.Create<ZoneVisualizationObject>(SelectObject);
        SelectCollisionCommand = ReactiveCommand.Create<CollisionVisualizationObject>(SelectCollision);
        SelectPathCommand = ReactiveCommand.Create<PathVisualizationObject>(SelectPath);
        SelectMeshCommand = ReactiveCommand.Create<NifMeshVisualizationObject>(SelectMesh);
        SelectVolumeCommand = ReactiveCommand.Create<VolumeVisualizationObject>(SelectVolume);
        SelectTriggerCommand = ReactiveCommand.Create<TriggerVisualizationObject>(SelectTrigger);
        SelectRelatedTriggerCommand = ReactiveCommand.Create<TriggerVisualizationObject>(SelectRelatedTrigger);
        SelectRelatedVolumeCommand = ReactiveCommand.Create<VolumeVisualizationObject>(SelectRelatedVolume);
        SelectZoneCommand = ReactiveCommand.Create(SelectZone);
        EditNpcInventoryCommand = ReactiveCommand.Create(EditNpcInventory);
        EditNpcSpellInventoryCommand = ReactiveCommand.Create(EditNpcSpellInventory);
        EditCreatureDeckCommand = ReactiveCommand.CreateFromTask(EditCreatureDeck);
        ViewDropTableCommand = ReactiveCommand.Create<string>(ViewDropTable);
        CreateDropTableCommand = ReactiveCommand.Create<string>(CreateDropTable);
        OpenOrCreateDropTableCommand = ReactiveCommand.Create<string>(OpenOrCreateDropTable);
        
        // Initialize viewport commands
        ZoomInCommand = ReactiveCommand.Create(ZoomIn);
        ZoomOutCommand = ReactiveCommand.Create(ZoomOut);
        ResetViewCommand = ReactiveCommand.Create(ResetView);
        PanUpCommand = ReactiveCommand.Create(PanUp);
        PanDownCommand = ReactiveCommand.Create(PanDown);
        PanLeftCommand = ReactiveCommand.Create(PanLeft);
        PanRightCommand = ReactiveCommand.Create(PanRight);

        // Initialize export command
        ExportSelectedAsJsonCommand = ReactiveCommand.Create(ExportSelectedAsJson);

        // Initialize teleport database commands
        EditTeleportCommand = ReactiveCommand.Create<ResultWrapper>(EditTeleport);
        CreateTeleportCommand = ReactiveCommand.Create<ResultWrapper>(CreateTeleport);
        
        Zones = new ObservableCollection<string>();
        ZoneObjects = new ObservableCollection<ZoneObjectItem>();
        Shopkeepers = new ObservableCollection<ShopkeeperItem>();
        Professors = new ObservableCollection<ProfessorItem>();
        ZoneTransfers = new ObservableCollection<ZoneTransferItem>();
        ZoneVisualizationObjects = new ObservableCollection<ZoneVisualizationObject>();
        CollisionVisualizationObjects = new ObservableCollection<CollisionVisualizationObject>();
        NifMeshVisualizationObjects = new ObservableCollection<NifMeshVisualizationObject>();
        SpawnVisualizationObjects = new ObservableCollection<SpawnVisualizationObject>();
        PathVisualizationObjects = new ObservableCollection<PathVisualizationObject>();
        NodeVisualizationObjects = new ObservableCollection<NodeVisualizationObject>();
        VolumeVisualizationObjects = new ObservableCollection<VolumeVisualizationObject>();
        TriggerVisualizationObjects = new ObservableCollection<TriggerVisualizationObject>();
        
        _nifProcessor = new NifGeometryProcessor();
        
        // Initialize viewport to show coordinates (0,0) in the center
        // Also ensure the initial zoom transform is applied
        InitializeViewportCenter();
        
        // Apply initial zoom transform (since field initialization doesn't trigger property setter)
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateCanvasTransform();
        }, Avalonia.Threading.DispatcherPriority.Loaded);
        
        LoadAvailableZones();

        // Check world database availability at startup
        _ = System.Threading.Tasks.Task.Run(CheckWorldDatabaseAvailabilityAsync);

        // Auto-prompt for zone selection on first open of a brand new Zone Editor tab
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!_autoPromptedZoneSelection && string.IsNullOrEmpty(SelectedZone))
            {
                _autoPromptedZoneSelection = true;
                LoadZone();
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    public ObservableCollection<string> Zones { get; }
    public ObservableCollection<ZoneObjectItem> ZoneObjects { get; }
    public ObservableCollection<ShopkeeperItem> Shopkeepers { get; }
    public ObservableCollection<ProfessorItem> Professors { get; }
    public ObservableCollection<ZoneTransferItem> ZoneTransfers { get; }
    public ObservableCollection<ZoneVisualizationObject> ZoneVisualizationObjects { get; }
    public ObservableCollection<CollisionVisualizationObject> CollisionVisualizationObjects { get; }
    public ObservableCollection<NifMeshVisualizationObject> NifMeshVisualizationObjects { get; }
    public ObservableCollection<SpawnVisualizationObject> SpawnVisualizationObjects { get; }
    public ObservableCollection<PathVisualizationObject> PathVisualizationObjects { get; }
    public ObservableCollection<NodeVisualizationObject> NodeVisualizationObjects { get; }
    public ObservableCollection<VolumeVisualizationObject> VolumeVisualizationObjects { get; }
    public ObservableCollection<TriggerVisualizationObject> TriggerVisualizationObjects { get; }

    public string SelectedZone
    {
        get => _selectedZone;
        set {
            Console.WriteLine($"[DEBUG ZONE VM] SelectedZone changed to: '{value ?? "null"}'");
            this.RaiseAndSetIfChanged(ref _selectedZone, value);
        }
    }

    public ZoneVisualizationObject? SelectedObject
    {
        get => _selectedObject;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedObject, value);
            
            // Update SelectedCoreObject when a zone object is selected
            if (value != null && _currentZoneData?.m_objectList != null)
            {
                SelectedCoreObject = _currentZoneData.m_objectList
                    .FirstOrDefault(obj => obj != null && (ulong)obj.m_templateID == value.TemplateID);
            }
            else
            {
                SelectedCoreObject = null;
            }
            
            // Clear other selections when selecting a zone object
            if (value != null)
            {
                SelectedCollision = null;
                SelectedPath = null;
                SelectedMesh = null;
                SelectedVolume = null;
                SelectedTrigger = null;
                // Important: clear any lingering spawn selection so its panel doesn't remain visible
                SelectedSpawn = null;
                SelectedSpawnTemplate = null;
            }
            
            // Notify property changes
            this.RaisePropertyChanged(nameof(HasSelectedObject));
            this.RaisePropertyChanged(nameof(HasSelectedAnyObject));
            this.RaisePropertyChanged(nameof(IsSelectedObjectNpc));
            this.RaisePropertyChanged(nameof(HasSelectedSpawn));
            NotifySelectionChanged();
            
            // Update visual selection in the viewport
            UpdateVisualSelection();
        }
    }

    public CoreObjectInfo? SelectedCoreObject
    {
        get => _selectedCoreObject;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCoreObject, value);
            
            // Load template data when a core object is selected
            if (value != null)
            {
                _ = Task.Run(async () =>
                {
                    var template = await LoadTemplateAsync(value);
                    SelectedTemplate = template;
                });
            }
            else
            {
                SelectedTemplate = null;
            }
        }
    }
    
    public PropertyClass? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTemplate, value);
            this.RaisePropertyChanged(nameof(SelectedTemplateTypeName));
            this.RaisePropertyChanged(nameof(SelectedGameObjectTemplate));
            this.RaisePropertyChanged(nameof(SelectedGameObjectTemplateBehaviors));
            this.RaisePropertyChanged(nameof(SelectedTemplateDisplayNameResolved));
            this.RaisePropertyChanged(nameof(SelectedTemplateDisplayNameLocaleId));
            this.RaisePropertyChanged(nameof(SelectedTemplateDescriptionResolved));
            this.RaisePropertyChanged(nameof(SelectedTemplateDescriptionLocaleId));
            this.RaisePropertyChanged(nameof(SelectedTemplateObjectNameResolved));
            this.RaisePropertyChanged(nameof(SelectedTemplateObjectNameLocaleId));
            this.RaisePropertyChanged(nameof(SelectedTemplateLootTables));
            this.RaisePropertyChanged(nameof(HasLootTables));
            this.RaisePropertyChanged(nameof(SelectedTemplateLootTableItems));

            // Update dependent visibility properties
            this.RaisePropertyChanged(nameof(ShowSpawnDeckSection));
            this.RaisePropertyChanged(nameof(ShowSpawnLootTablesSection));
            
            // Ensure drop table cache is loaded so labels can reflect existence
            _ = EnsureDropTableCacheAsync();
        }
    }
    
    public string SelectedTemplateTypeName => SelectedTemplate?.GetType().Name ?? "Unknown";
    
    public GameObjectTemplate? SelectedGameObjectTemplate => SelectedTemplate as GameObjectTemplate;
    
    /// <summary>
    /// Gets the resolved English display name from the locale service for the selected template.
    /// Returns null if no template is selected or locale resolution fails.
    /// </summary>
    public string? SelectedTemplateDisplayNameResolved
    {
        get
        {
            var displayName = SelectedGameObjectTemplate?.m_displayName;
            if (string.IsNullOrWhiteSpace(displayName) || !LocaleService.Instance.IsLoaded)
            {
                return null;
            }
            
            return ResolveLocaleString(displayName);
        }
    }
    
    /// <summary>
    /// Gets the raw locale ID for the selected template's display name.
    /// </summary>
    public string? SelectedTemplateDisplayNameLocaleId
    {
        get
        {
            var displayName = SelectedGameObjectTemplate?.m_displayName;
            return string.IsNullOrWhiteSpace(displayName) ? null : displayName;
        }
    }
    
    /// <summary>
    /// Gets the resolved English description from the locale service for the selected template.
    /// Returns null if no template is selected or locale resolution fails.
    /// </summary>
    public string? SelectedTemplateDescriptionResolved
    {
        get
        {
            var description = SelectedGameObjectTemplate?.m_description;
            if (string.IsNullOrWhiteSpace(description) || !LocaleService.Instance.IsLoaded)
            {
                return null;
            }
            
            return ResolveLocaleString(description);
        }
    }
    
    /// <summary>
    /// Gets the raw locale ID for the selected template's description.
    /// </summary>
    public string? SelectedTemplateDescriptionLocaleId
    {
        get
        {
            var description = SelectedGameObjectTemplate?.m_description;
            return string.IsNullOrWhiteSpace(description) ? null : description;
        }
    }
    
    /// <summary>
    /// Gets the resolved English object name from the locale service for the selected template.
    /// Returns null if no template is selected or locale resolution fails.
    /// </summary>
    public string? SelectedTemplateObjectNameResolved
    {
        get
        {
            var objectName = SelectedGameObjectTemplate?.m_objectName;
            if (string.IsNullOrWhiteSpace(objectName) || !LocaleService.Instance.IsLoaded)
            {
                return null;
            }
            
            return ResolveLocaleString(objectName);
        }
    }
    
    /// <summary>
    /// Gets the raw locale ID for the selected template's object name.
    /// </summary>
    public string? SelectedTemplateObjectNameLocaleId
    {
        get
        {
            var objectName = SelectedGameObjectTemplate?.m_objectName;
            return string.IsNullOrWhiteSpace(objectName) ? null : objectName;
        }
    }
    
    public ObservableCollection<BehaviorWrapper> SelectedGameObjectTemplateBehaviors
    {
        get
        {
            var behaviors = new ObservableCollection<BehaviorWrapper>();
            if (SelectedGameObjectTemplate?.m_behaviors != null)
            {
                foreach (var behavior in SelectedGameObjectTemplate.m_behaviors)
                {
                    if (behavior != null)
                    {
                        behaviors.Add(new BehaviorWrapper(behavior));
                    }
                }
            }
            return behaviors;
        }
    }
    
    /// <summary>
    /// Gets the loot tables from the selected template if it has an m_lootTable property.
    /// Returns null if no template is selected or the template doesn't have loot tables.
    /// </summary>
    public List<string>? SelectedTemplateLootTables
    {
        get
        {
            return GetLootTablesFromTemplate(SelectedTemplate);
        }
    }
    
    // Loot table items with existence info (for UI)
    public ObservableCollection<LootTableItem> SelectedTemplateLootTableItems
    {
        get
        {
            var items = new ObservableCollection<LootTableItem>();
            var names = SelectedTemplateLootTables;
            if (names != null)
            {
                foreach (var name in names)
                {
                    var exists = _existingDropTableNames.Contains(name);
                    items.Add(new LootTableItem(name, exists));
                }
            }
            return items;
        }
    }
    
    /// <summary>
    /// Gets whether the selected template has loot tables.
    /// </summary>
    public bool HasLootTables => SelectedTemplateLootTables?.Count > 0;

    public CollisionVisualizationObject? SelectedCollision
    {
        get => _selectedCollision;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCollision, value);
            
            // Clear other selections when selecting a collision object
            if (value != null)
            {
                SelectedObject = null;
                SelectedCoreObject = null;
                SelectedPath = null;
                SelectedMesh = null;
                SelectedVolume = null;
                SelectedTrigger = null;
                SelectedSpawn = null;
                SelectedSpawnTemplate = null;
            }
            
            this.RaisePropertyChanged(nameof(HasSelectedCollision));
            this.RaisePropertyChanged(nameof(HasSelectedAnyObject));
            this.RaisePropertyChanged(nameof(HasSelectedSpawn)); // Add explicit notification
            NotifySelectionChanged();
        }
    }

    public PathVisualizationObject? SelectedPath
    {
        get => _selectedPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPath, value);
            
            // Clear other selections when selecting a path object
            if (value != null)
            {
                SelectedObject = null;
                SelectedCoreObject = null;
                SelectedCollision = null;
                SelectedMesh = null;
                SelectedVolume = null;
                SelectedTrigger = null;
                SelectedSpawn = null;
                SelectedNode = null;
            }
            
            this.RaisePropertyChanged(nameof(HasSelectedPath));
            this.RaisePropertyChanged(nameof(HasSelectedAnyObject));
            NotifySelectionChanged();
        }
    }

    public NodeVisualizationObject? SelectedNode { get; set; }

    // A generic binding target for TreeView selection in the hierarchy
    public object? SelectedHierarchyItem
    {
        get => _selectedHierarchyItem;
        set
        {
            if (!ReferenceEquals(_selectedHierarchyItem, value))
            {
                _selectedHierarchyItem = value;
                // Route selection to specific properties based on type
                if (value is PathVisualizationObject path)
                {
                    // Use the existing selection helper to ensure proper notifications
                    SelectPath(path);
                }
                else if (value is NodeVisualizationObject node)
                {
                    ClearAllSelections();
                    SelectedNode = node;
                    // Notify full selection change to refresh panels
                    NotifySelectionChanged();
                }
                else if (value is SpawnVisualizationObject spawn)
                {
                    // Use helper to set selection and trigger template load + notifications
                    SelectSpawn(spawn);
                }
                else if (value == null)
                {
                    ClearAllSelections();
                }
                this.RaisePropertyChanged(nameof(SelectedHierarchyItem));
            }
        }
    }

    public NifMeshVisualizationObject? SelectedMesh
    {
        get => _selectedMesh;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedMesh, value);
            
            // Clear other selections when selecting a mesh object
            if (value != null)
            {
                SelectedObject = null;
                SelectedCoreObject = null;
                SelectedCollision = null;
                SelectedPath = null;
                SelectedVolume = null;
                SelectedTrigger = null;
                SelectedSpawn = null;
                SelectedSpawnTemplate = null;
            }
            
            this.RaisePropertyChanged(nameof(HasSelectedMesh));
            this.RaisePropertyChanged(nameof(HasSelectedAnyObject));
            this.RaisePropertyChanged(nameof(HasSelectedSpawn)); // Add explicit notification
            NotifySelectionChanged();
        }
    }

    public VolumeVisualizationObject? SelectedVolume
    {
        get => _selectedVolume;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedVolume, value);
            
            // Clear other selections when selecting a volume object
            if (value != null)
            {
                SelectedObject = null;
                SelectedCoreObject = null;
                SelectedCollision = null;
                SelectedPath = null;
                SelectedMesh = null;
                SelectedTrigger = null;
                SelectedSpawn = null;
                SelectedSpawnTemplate = null;
            }
            
            this.RaisePropertyChanged(nameof(HasSelectedVolume));
            this.RaisePropertyChanged(nameof(HasSelectedAnyObject));
            this.RaisePropertyChanged(nameof(HasSelectedSpawn)); // Add explicit notification
            this.RaisePropertyChanged(nameof(SelectedVolumeEnterTriggers));
            this.RaisePropertyChanged(nameof(SelectedVolumeExitTriggers));
            NotifySelectionChanged();
        }
    }

    public SpawnVisualizationObject? SelectedSpawn
    {
        get => _selectedSpawn;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSpawn, value);
            
            if (value != null)
            {
                SelectedObject = null;
                SelectedCollision = null;
                SelectedPath = null;
                SelectedMesh = null;
                SelectedVolume = null;
                SelectedTrigger = null;
                
                // Clear any previous spawn template immediately so UI shows loading state
                SelectedSpawnTemplate = null;
                var selectedSpawnId = value.SpawnId;
                var templateId = value.TemplateId;
                
                // Load template for the selected spawn
                _ = Task.Run(async () =>
                {
                    var template = await LoadSpawnTemplateAsync(templateId);
                    // Switch back to UI thread to update bindings safely
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        // Only apply if selection hasn't changed
                        if (SelectedSpawn != null && SelectedSpawn.SpawnId == selectedSpawnId)
                        {
                            SelectedSpawnTemplate = template;
                        }
                    }, Avalonia.Threading.DispatcherPriority.Background);
                });
            }
            else
            {
                // When deselecting spawns, ensure template section is cleared
                SelectedSpawnTemplate = null;
            }

            this.RaisePropertyChanged(nameof(HasSelectedSpawn));
            this.RaisePropertyChanged(nameof(HasSelectedAnyObject));
            NotifySelectionChanged();
        }
    }

    public TriggerVisualizationObject? SelectedTrigger
    {
        get => _selectedTrigger;
        set
        {
            Console.WriteLine($"[DEBUG ZONE VM] SelectedTrigger changed to: '{value?.Name ?? "null"}'");
            this.RaiseAndSetIfChanged(ref _selectedTrigger, value);
            
            // Clear other selections when selecting a trigger object
            if (value != null)
            {
                SelectedObject = null;
                SelectedCoreObject = null;
                SelectedCollision = null;
                SelectedPath = null;
                SelectedMesh = null;
                SelectedVolume = null;
                SelectedSpawn = null;
                SelectedSpawnTemplate = null;
            }
            
            this.RaisePropertyChanged(nameof(HasSelectedTrigger));
            this.RaisePropertyChanged(nameof(HasSelectedAnyObject));
            this.RaisePropertyChanged(nameof(HasSelectedSpawn)); // Add explicit notification
            this.RaisePropertyChanged(nameof(SelectedTriggerActivatingVolumes));
            this.RaisePropertyChanged(nameof(SelectedTriggerDeactivatingVolumes));
            this.RaisePropertyChanged(nameof(SelectedTriggerFireVolumes));
            NotifySelectionChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool ShowCollisions
    {
        get => _showCollisions;
        set
        {
            this.RaiseAndSetIfChanged(ref _showCollisions, value);
            UpdateVisibility();
        }
    }

    public bool ShowZoneObjects
    {
        get => _showZoneObjects;
        set
        {
            this.RaiseAndSetIfChanged(ref _showZoneObjects, value);
            UpdateVisibility();
        }
    }

    public bool ShowNifGeometry
    {
        get => _showNifGeometry;
        set
        {
            this.RaiseAndSetIfChanged(ref _showNifGeometry, value);
            UpdateVisibility();
        }
    }

    public bool ShowSpawns
    {
        get => _showSpawns;
        set
        {
            this.RaiseAndSetIfChanged(ref _showSpawns, value);
            this.RaisePropertyChanged(nameof(ShowPathsAndSpawns));
            UpdateVisibility();
        }
    }

    public bool ShowPaths
    {
        get => _showPaths;
        set
        {
            this.RaiseAndSetIfChanged(ref _showPaths, value);
            this.RaisePropertyChanged(nameof(ShowPathsAndSpawns));
            UpdateVisibility();
        }
    }

    // Convenience filter to toggle both Paths and Spawns from a single checkbox
    public bool ShowPathsAndSpawns
    {
        get => ShowPaths && ShowSpawns;
        set
        {
            // Set both flags together
            if (ShowPaths != value)
                ShowPaths = value;
            if (ShowSpawns != value)
                ShowSpawns = value;
            // Notify combined property for UI
            this.RaisePropertyChanged(nameof(ShowPathsAndSpawns));
            UpdateVisibility();
        }
    }

    public bool ShowNodes
    {
        get => _showNodes;
        set
        {
            this.RaiseAndSetIfChanged(ref _showNodes, value);
            UpdateVisibility();
        }
    }

    public bool ShowVolumes
    {
        get => _showVolumes;
        set
        {
            this.RaiseAndSetIfChanged(ref _showVolumes, value);
            UpdateVisibility();
        }
    }

    public bool ShowTriggers
    {
        get => _showTriggers;
        set
        {
            this.RaiseAndSetIfChanged(ref _showTriggers, value);
            UpdateVisibility();
        }
    }

    // Collision shape filter properties
    public bool ShowBoxCollisions
    {
        get => _showBoxCollisions;
        set
        {
            this.RaiseAndSetIfChanged(ref _showBoxCollisions, value);
            UpdateVisibility();
        }
    }

    public bool ShowSphereCollisions
    {
        get => _showSphereCollisions;
        set
        {
            this.RaiseAndSetIfChanged(ref _showSphereCollisions, value);
            UpdateVisibility();
        }
    }

    public bool ShowCylinderCollisions
    {
        get => _showCylinderCollisions;
        set
        {
            this.RaiseAndSetIfChanged(ref _showCylinderCollisions, value);
            UpdateVisibility();
        }
    }

    public bool ShowTubeCollisions
    {
        get => _showTubeCollisions;
        set
        {
            this.RaiseAndSetIfChanged(ref _showTubeCollisions, value);
            UpdateVisibility();
        }
    }

    public bool ShowPlaneCollisions
    {
        get => _showPlaneCollisions;
        set
        {
            this.RaiseAndSetIfChanged(ref _showPlaneCollisions, value);
            UpdateVisibility();
        }
    }

    public bool ShowMeshCollisions
    {
        get => _showMeshCollisions;
        set
        {
            this.RaiseAndSetIfChanged(ref _showMeshCollisions, value);
            UpdateVisibility();
        }
    }

    public bool ShowRayCollisions
    {
        get => _showRayCollisions;
        set
        {
            this.RaiseAndSetIfChanged(ref _showRayCollisions, value);
            UpdateVisibility();
        }
    }

    public bool HasSelectedObject => SelectedObject != null;
    public bool HasSelectedCollision => SelectedCollision != null;
    public bool HasSelectedPath => SelectedPath != null;
    public bool HasSelectedMesh => SelectedMesh != null;
    public bool HasSelectedVolume => SelectedVolume != null;
    public bool HasSelectedTrigger => SelectedTrigger != null;
    public bool HasSelectedSpawn => SelectedSpawn != null;
    public bool HasSelectedNode => SelectedNode != null;
    public bool HasSelectedAnyObject => HasSelectedObject || HasSelectedCollision || HasSelectedPath || HasSelectedMesh || HasSelectedVolume || HasSelectedTrigger || HasSelectedSpawn || HasSelectedNode;
    
    // Path Details Properties for Properties Panel
    public string SelectedPathName => SelectedPath?.Name ?? "No Path Selected";
    public string SelectedPathId => SelectedPath != null ? $"Path ID: {SelectedPath.PathId}" : "";
    public string SelectedPathType => SelectedPath?.PathType ?? "";
    public string SelectedPathNodeCount => SelectedPath != null ? $"Nodes: {SelectedPath.Nodes.Count}" : "";
    public string SelectedPathSpawnCount => SelectedPath != null ? GetPathSpawnCount(SelectedPath) : "";
    public string SelectedPathDescription => SelectedPath != null ? GetPathDetailedDescription(SelectedPath) : "";
    public ObservableCollection<NodeVisualizationObject> SelectedPathNodes => 
        SelectedPath?.Nodes != null ? new ObservableCollection<NodeVisualizationObject>(SelectedPath.Nodes) : new ObservableCollection<NodeVisualizationObject>();
    public ObservableCollection<SpawnVisualizationObject> SelectedPathSpawns =>
        SelectedPath != null ? new ObservableCollection<SpawnVisualizationObject>(GetSpawnsForPath(SelectedPath)) : new ObservableCollection<SpawnVisualizationObject>();
    
    // All Path Properties via reflection (similar to node)
    public List<NodePropertyItem> SelectedPathAllProperties
    {
        get
        {
            var list = new List<NodePropertyItem>();
            var obj = SelectedPath?.OriginalPathObject;
            if (obj == null) return list;
            try
            {
                var type = obj.GetType();
                list.Add(new NodePropertyItem { Name = "Type", Value = type.Name });
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanRead) continue;
                    var val = prop.GetValue(obj);
                    string str = FormatNodePropertyValue(val);
                    list.Add(new NodePropertyItem { Name = prop.Name, Value = str });
                }
            }
            catch (Exception ex)
            {
                list.Add(new NodePropertyItem { Name = "Error", Value = ex.Message });
            }
            return list;
        }
    }
    
    // Spawn Details Properties for Properties Panel
    public PropertyClass? SelectedSpawnTemplate
    {
        get => _selectedSpawnTemplate;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSpawnTemplate, value);
            
            // Notify dependent properties so the UI updates when the template finishes loading
            this.RaisePropertyChanged(nameof(SelectedSpawnGameObjectTemplate));
            this.RaisePropertyChanged(nameof(SelectedSpawnTemplateClassName));
            this.RaisePropertyChanged(nameof(SelectedSpawnTemplateIcon));
            this.RaisePropertyChanged(nameof(SelectedSpawnTemplateLevel));
            this.RaisePropertyChanged(nameof(SelectedSpawnTemplatePrimarySchool));
            this.RaisePropertyChanged(nameof(SelectedSpawnTemplateObjectName));
            this.RaisePropertyChanged(nameof(SelectedSpawnTemplateDisplayName));
            this.RaisePropertyChanged(nameof(SelectedSpawnTemplateDescription));
            this.RaisePropertyChanged(nameof(SelectedSpawnTemplateLootTables));
            this.RaisePropertyChanged(nameof(HasSpawnLootTables));
            this.RaisePropertyChanged(nameof(SelectedSpawnTemplateLootTableItems));
            
            // Update creature deck detection asynchronously
            _ = UpdateCreatureDeckDetectionAsync();
            
            // Ensure drop table cache is loaded so labels can reflect existence
            _ = EnsureDropTableCacheAsync();
        }
    }
    public string SelectedSpawnName => SelectedSpawn?.Name ?? "No Spawn Selected";
    public string SelectedSpawnId => SelectedSpawn != null ? $"Spawn ID: {SelectedSpawn.SpawnId}" : "";
    public string SelectedSpawnTemplateId => SelectedSpawn != null ? $"Template ID: {SelectedSpawn.TemplateId}" : "";
    public string SelectedSpawnType => SelectedSpawn?.CreatureType ?? "";
    public string SelectedSpawnPath => SelectedSpawn != null && SelectedSpawn.PathId != 0 ? $"Path ID: {SelectedSpawn.PathId}" : "No Path";
    public string SelectedSpawnLocation => SelectedSpawn != null ? $"Position: ({SelectedSpawn.X:F1}, {SelectedSpawn.Y:F1}, {SelectedSpawn.Z:F1})" : "";
    public string SelectedSpawnChance => SelectedSpawn != null ? $"Spawn Chance: {SelectedSpawn.SpawnChance}%" : "";
    
    // Template properties for spawn (reuse existing template display logic)
    public GameObjectTemplate? SelectedSpawnGameObjectTemplate => SelectedSpawnTemplate as GameObjectTemplate;
    
    // Safe computed properties for spawn template details
    public string? SelectedSpawnTemplateClassName => GetTemplateProperty(SelectedSpawnTemplate, "m_className");
    public string? SelectedSpawnTemplateIcon => GetTemplateProperty(SelectedSpawnTemplate, "m_icon");
    public string? SelectedSpawnTemplateLevel => GetTemplateProperty(SelectedSpawnTemplate, "m_level");
    public string? SelectedSpawnTemplatePrimarySchool => GetTemplateProperty(SelectedSpawnTemplate, "m_primarySchool");
    public string? SelectedSpawnTemplateObjectName => GetTemplateProperty(SelectedSpawnTemplate, "m_objectName");
    public string? SelectedSpawnTemplateDisplayName => GetTemplateProperty(SelectedSpawnTemplate, "m_displayName");
    public string? SelectedSpawnTemplateDescription => GetTemplateProperty(SelectedSpawnTemplate, "m_description");
    
    // Spawn template loot tables (same pattern as zone objects)
    public List<string>? SelectedSpawnTemplateLootTables => GetLootTablesFromTemplate(SelectedSpawnTemplate);
    public bool HasSpawnLootTables => SelectedSpawnTemplateLootTables?.Count > 0;
    
    // Loot table items with existence info for spawn templates
    public ObservableCollection<LootTableItem> SelectedSpawnTemplateLootTableItems
    {
        get
        {
            var items = new ObservableCollection<LootTableItem>();
            var names = SelectedSpawnTemplateLootTables;
            if (names != null)
            {
                foreach (var name in names)
                {
                    var exists = _existingDropTableNames.Contains(name);
                    items.Add(new LootTableItem(name, exists));
                }
            }
            return items;
        }
    }
    
    private string? _selectedSpawnDeckName;
    
    // Spawn template creature deck detection (following CombatCreatureDeckComponent logic)
    public string? SelectedSpawnDeckName 
    { 
        get 
        { 
            Console.WriteLine($"[DECK DEBUG] SelectedSpawnDeckName accessed, cached value: {_selectedSpawnDeckName ?? "null"}");
            return _selectedSpawnDeckName;
        } 
        private set
        {
            _selectedSpawnDeckName = value;
            this.RaisePropertyChanged(nameof(SelectedSpawnDeckName));
            this.RaisePropertyChanged(nameof(HasSpawnDeck));
        }
    }
    public bool HasSpawnDeck 
    { 
        get 
        { 
            var deckName = SelectedSpawnDeckName;
            var result = !string.IsNullOrEmpty(deckName);
            Console.WriteLine($"[DECK DEBUG] HasSpawnDeck returning: {result} (deck name: {deckName ?? "null"})");
            return result;
        } 
    }
    
    private string _spawnDeckButtonText = "Edit Deck";
    
    /// <summary>
    /// Gets the button text for the creature deck action (Create or Edit)
    /// </summary>
    public string SpawnDeckButtonText 
    {
        get => _spawnDeckButtonText;
        private set
        {
            _spawnDeckButtonText = value;
            this.RaisePropertyChanged(nameof(SpawnDeckButtonText));
        }
    }
    
    /// <summary>
    /// Checks if the creature deck exists in the database
    /// </summary>
    public async Task<bool> SpawnDeckExistsAsync()
    {
        if (string.IsNullOrEmpty(SelectedSpawnDeckName))
            return false;
            
        try
        {
            return await CreatureDeckService.HasCreatureDeckAsync(SelectedSpawnDeckName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DECK DEBUG] Error checking if deck exists: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Detects if the spawn template has a creature deck following the exact logic from CombatCreatureDeckComponent
    /// </summary>
    private async Task<string?> GetCreatureDeckNameAsync(GameObjectTemplate? template)
    {
        Console.WriteLine($"[DECK DEBUG] GetCreatureDeckName called with template: {template?.GetType()?.Name ?? "null"}");
        
        if (template?.m_behaviors == null) 
        {
            Console.WriteLine($"[DECK DEBUG] Template is null or has no behaviors");
            return null;
        }
        
        Console.WriteLine($"[DECK DEBUG] Template has {template.m_behaviors.Count} behaviors");
        
        // Log all behavior types for debugging
        foreach (var behavior in template.m_behaviors)
        {
            Console.WriteLine($"[DECK DEBUG] Found behavior: {behavior?.GetType()?.Name ?? "null"}");
        }
        
        // Check if has both NPCBehaviorTemplate and DuelistBehaviorTemplate (required for dueling creatures)
        var hasNpcBehavior = template.m_behaviors.Any(b => b?.GetType().Name == "NPCBehaviorTemplate");
        var hasDuelistBehavior = template.m_behaviors.Any(b => b?.GetType().Name == "DuelistBehaviorTemplate");
        
        Console.WriteLine($"[DECK DEBUG] Has NPCBehaviorTemplate: {hasNpcBehavior}");
        Console.WriteLine($"[DECK DEBUG] Has DuelistBehaviorTemplate: {hasDuelistBehavior}");
        
        if (!hasNpcBehavior || !hasDuelistBehavior) 
        {
            Console.WriteLine($"[DECK DEBUG] Missing required behaviors - not a dueling creature");
            return null;
        }
        
        Console.WriteLine($"[DECK DEBUG] Found both NPC and Duelist behaviors - this is a dueling creature!");
        
        // Find EquipmentBehaviorTemplate and get m_itemList
        var equipmentBehavior = template.m_behaviors.FirstOrDefault(b => b?.GetType().Name == "EquipmentBehaviorTemplate");
        Console.WriteLine($"[DECK DEBUG] EquipmentBehaviorTemplate found: {equipmentBehavior != null}");
        
        if (equipmentBehavior == null) 
        {
            Console.WriteLine($"[DECK DEBUG] No EquipmentBehaviorTemplate found");
            return null;
        }
        
        // Get m_itemList property using reflection (since it's generated code)
        var itemListProperty = equipmentBehavior.GetType().GetProperty("m_itemList");
        Console.WriteLine($"[DECK DEBUG] m_itemList property found: {itemListProperty != null}");
        
        if (itemListProperty?.GetValue(equipmentBehavior) is not List<uint> itemList || !itemList.Any())
        {
            Console.WriteLine($"[DECK DEBUG] m_itemList is null or empty");
            return null;
        }
        
        Console.WriteLine($"[DECK DEBUG] Found {itemList.Count} items in equipment list: [{string.Join(", ", itemList)}]");
        
        // Load each item template and look for DeckBehaviorTemplate
        foreach (var itemId in itemList)
        {
            try
            {
                Console.WriteLine($"[DECK DEBUG] Loading item template {itemId}...");
                var itemTemplate = await LoadItemTemplateAsync(itemId);
                
                if (itemTemplate?.m_behaviors != null)
                {
                    Console.WriteLine($"[DECK DEBUG] Item template {itemId} has {itemTemplate.m_behaviors.Count} behaviors");
                    
                    var deckBehavior = itemTemplate.m_behaviors.FirstOrDefault(b => b?.GetType().Name == "DeckBehaviorTemplate");
                    if (deckBehavior != null)
                    {
                        Console.WriteLine($"[DECK DEBUG] Found DeckBehaviorTemplate in item {itemId}!");
                        
                        var defaultDeckProperty = deckBehavior.GetType().GetProperty("m_defaultDeck");
                        if (defaultDeckProperty?.GetValue(deckBehavior) is string deckName && !string.IsNullOrEmpty(deckName))
                        {
                            Console.WriteLine($"[DECK DEBUG] Found deck name: {deckName}");
                            return deckName;
                        }
                        else
                        {
                            Console.WriteLine($"[DECK DEBUG] DeckBehaviorTemplate found but m_defaultDeck is null or empty");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[DECK DEBUG] Item template {itemId} has no behaviors");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DECK DEBUG] Error loading item template {itemId}: {ex.Message}");
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Updates the creature deck detection asynchronously
    /// </summary>
    private async Task UpdateCreatureDeckDetectionAsync()
    {
        try
        {
            var deckName = await GetCreatureDeckNameAsync(SelectedSpawnGameObjectTemplate);
            SelectedSpawnDeckName = deckName;
            
            // Check if deck exists in database and update button text
            if (!string.IsNullOrEmpty(deckName))
            {
                var deckExists = await CreatureDeckService.HasCreatureDeckAsync(deckName);
                SpawnDeckButtonText = deckExists ? "Edit Deck" : "Create Deck";
                Console.WriteLine($"[DECK DEBUG] Deck '{deckName}' exists in database: {deckExists}, button text: {SpawnDeckButtonText}");
            }
            else
            {
                SpawnDeckButtonText = "Edit Deck";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DECK DEBUG] Error updating deck detection: {ex.Message}");
            SelectedSpawnDeckName = null;
            SpawnDeckButtonText = "Edit Deck";
        }
    }
    
    /// <summary>
    /// Loads an item template by its template ID for deck detection
    /// </summary>
    /// <param name="itemTemplateId">The item template ID to load</param>
    /// <returns>The loaded item template as GameObjectTemplate, or null if not found</returns>
    private async Task<GameObjectTemplate?> LoadItemTemplateAsync(uint itemTemplateId)
    {
        try
        {
            // First, ensure template manifest is loaded
            var manifestService = TemplateManifestService.Instance;
            if (!manifestService.IsLoaded)
            {
                var templateManifestData = await RootWadService.Instance.GetFileAsync("TemplateManifest.xml");
                if (templateManifestData == null || !templateManifestData.HasValue)
                {
                    Console.WriteLine($"[DECK DEBUG] Failed to load TemplateManifest.xml from Root.wad");
                    return null;
                }
                
                if (!manifestService.LoadFromFileData(templateManifestData.Value))
                {
                    Console.WriteLine($"[DECK DEBUG] Failed to parse TemplateManifest.xml");
                    return null;
                }
            }
            
            // Find the template location by template ID
            var templateLocations = manifestService.GetAllTemplateLocations();
            var templateLocation = templateLocations.FirstOrDefault(t => t.m_id == itemTemplateId);
            
            if (templateLocation == null)
            {
                Console.WriteLine($"[DECK DEBUG] Item template with ID {itemTemplateId} not found in manifest");
                return null;
            }
            
            Console.WriteLine($"[DECK DEBUG] Loading item template: {templateLocation.m_filename} for ID {itemTemplateId}");
            
            // Load the template file from Root.wad
            var templateData = await RootWadService.Instance.GetFileAsync(templateLocation.m_filename);
            if (templateData == null || !templateData.HasValue)
            {
                Console.WriteLine($"[DECK DEBUG] Failed to load item template file '{templateLocation.m_filename}' from Root.wad");
                return null;
            }
            
            // Use the same BindSerializer instance and configuration as zone data loading
            var bindSerializer = new BindSerializer();
            var templateDataBytes = templateData.Value.ToArray();
            
            // Try GameObjectTemplate first (items should be GameObjectTemplates)
            if (bindSerializer.Deserialize<GameObjectTemplate>(templateDataBytes, 1, out var gameObjectTemplate) && gameObjectTemplate != null)
            {
                Console.WriteLine($"[DECK DEBUG] Successfully loaded item GameObjectTemplate: {templateLocation.m_filename}");
                return gameObjectTemplate;
            }
            
            Console.WriteLine($"[DECK DEBUG] Failed to deserialize item template from '{templateLocation.m_filename}'");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DECK DEBUG] Error loading item template ID {itemTemplateId}: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Gets whether the selected object is an NPC (has NPC flag)
    /// </summary>
    public bool IsSelectedObjectNpc
    {
        get
        {
            if (SelectedObject?.Flags == null) return false;
            return SelectedObject.Flags.Any(flag => flag.FlagType == "NPC");
        }
    }
    
    /// <summary>
    /// Gets triggers that are related to the currently selected volume's enter events
    /// </summary>
    public List<TriggerVisualizationObject> SelectedVolumeEnterTriggers
    {
        get
        {
            if (SelectedVolume?.EnterEvents == null || !SelectedVolume.EnterEvents.Any())
                return new List<TriggerVisualizationObject>();
                
            var relatedTriggers = new List<TriggerVisualizationObject>();
            foreach (var enterEvent in SelectedVolume.EnterEvents)
            {
                relatedTriggers.AddRange(FindTriggersForEvent(enterEvent));
            }
            return relatedTriggers.Distinct().ToList();
        }
    }
    
    /// <summary>
    /// Gets triggers that are related to the currently selected volume's exit events
    /// </summary>
    public List<TriggerVisualizationObject> SelectedVolumeExitTriggers
    {
        get
        {
            if (SelectedVolume?.ExitEvents == null || !SelectedVolume.ExitEvents.Any())
                return new List<TriggerVisualizationObject>();
                
            var relatedTriggers = new List<TriggerVisualizationObject>();
            foreach (var exitEvent in SelectedVolume.ExitEvents)
            {
                relatedTriggers.AddRange(FindTriggersForEvent(exitEvent));
            }
            return relatedTriggers.Distinct().ToList();
        }
    }
    
    /// <summary>
    /// Gets volumes that are related to the currently selected trigger's activate events
    /// </summary>
    public List<VolumeVisualizationObject> SelectedTriggerActivatingVolumes
    {
        get
        {
            if (SelectedTrigger?.ActivateEvents == null || !SelectedTrigger.ActivateEvents.Any())
                return new List<VolumeVisualizationObject>();
                
            var relatedVolumes = new List<VolumeVisualizationObject>();
            foreach (var activateEvent in SelectedTrigger.ActivateEvents)
            {
                relatedVolumes.AddRange(FindVolumesForEvent(activateEvent));
            }
            return relatedVolumes.Distinct().ToList();
        }
    }
    
    /// <summary>
    /// Gets volumes that are related to the currently selected trigger's deactivate events
    /// </summary>
    public List<VolumeVisualizationObject> SelectedTriggerDeactivatingVolumes
    {
        get
        {
            if (SelectedTrigger?.DeactivateEvents == null || !SelectedTrigger.DeactivateEvents.Any())
                return new List<VolumeVisualizationObject>();
                
            var relatedVolumes = new List<VolumeVisualizationObject>();
            foreach (var deactivateEvent in SelectedTrigger.DeactivateEvents)
            {
                relatedVolumes.AddRange(FindVolumesForEvent(deactivateEvent));
            }
            return relatedVolumes.Distinct().ToList();
        }
    }
    
    /// <summary>
    /// Gets volumes that are related to the currently selected trigger's fire events
    /// </summary>
    public List<VolumeVisualizationObject> SelectedTriggerFireVolumes
    {
        get
        {
            if (SelectedTrigger?.FireEvents == null || !SelectedTrigger.FireEvents.Any())
                return new List<VolumeVisualizationObject>();
                
            var relatedVolumes = new List<VolumeVisualizationObject>();
            foreach (var fireEvent in SelectedTrigger.FireEvents)
            {
                relatedVolumes.AddRange(FindVolumesForEvent(fireEvent));
            }
            return relatedVolumes.Distinct().ToList();
        }
    }

    // Computed properties for filter counts
    public int VisibleCollisionCount => CollisionVisualizationObjects.Count(ShouldShowCollision);
    public int BoxCollisionCount => CollisionVisualizationObjects.Count(c => c.GeometryType == "Box");
    public int SphereCollisionCount => CollisionVisualizationObjects.Count(c => c.GeometryType == "Sphere");
    public int CylinderCollisionCount => CollisionVisualizationObjects.Count(c => c.GeometryType == "Cylinder");
    public int TubeCollisionCount => CollisionVisualizationObjects.Count(c => c.GeometryType == "Tube");
    public int PlaneCollisionCount => CollisionVisualizationObjects.Count(c => c.GeometryType == "Plane");
    public int MeshCollisionCount => CollisionVisualizationObjects.Count(c => c.GeometryType == "Mesh");
    public int RayCollisionCount => CollisionVisualizationObjects.Count(c => c.GeometryType == "Ray");

    // Viewport properties
    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            value = Math.Max(MinZoom, Math.Min(MaxZoom, value));
            this.RaiseAndSetIfChanged(ref _zoomLevel, value);
            UpdateCanvasTransform();
        }
    }

    public double PanX
    {
        get => _panX;
        set
        {
            this.RaiseAndSetIfChanged(ref _panX, value);
            UpdateCanvasTransform();
        }
    }

    public double PanY
    {
        get => _panY;
        set
        {
            this.RaiseAndSetIfChanged(ref _panY, value);
            UpdateCanvasTransform();
        }
    }

    public string ZoomDisplayText => $"Zoom: {ZoomLevel:P0}";

    // World database gating properties
    public bool IsWorldDatabaseReady {
        get => _isWorldDatabaseReady;
        private set {
            this.RaiseAndSetIfChanged(ref _isWorldDatabaseReady, value);
            this.RaisePropertyChanged(nameof(NotWorldDatabaseReady));
            this.RaisePropertyChanged(nameof(ShowSpawnDeckSection));
            this.RaisePropertyChanged(nameof(ShowSpawnLootTablesSection));
            this.RaisePropertyChanged(nameof(ShowNpcDbActions));
        }
    }
    public bool NotWorldDatabaseReady => !IsWorldDatabaseReady;
    public string WorldDatabaseStatusText {
        get => _worldDatabaseStatusText;
        private set => this.RaiseAndSetIfChanged(ref _worldDatabaseStatusText, value);
    }

    public bool ShowSpawnDeckSection => HasSpawnDeck && IsWorldDatabaseReady;
    public bool ShowSpawnLootTablesSection => HasSpawnLootTables && IsWorldDatabaseReady;
    public bool ShowNpcDbActions => IsSelectedObjectNpc && IsWorldDatabaseReady;

    // Expose Vector3 components for XAML binding (Zone Objects)
    public float LocationX => SelectedCoreObject?.m_location.X ?? 0f;
    public float LocationY => SelectedCoreObject?.m_location.Y ?? 0f;  
    public float LocationZ => SelectedCoreObject?.m_location.Z ?? 0f;
    public float OrientationX => SelectedCoreObject?.m_orientation.X ?? 0f;
    public float OrientationY => SelectedCoreObject?.m_orientation.Y ?? 0f;
    public float OrientationZ => SelectedCoreObject?.m_orientation.Z ?? 0f;
    
    // Unified transform properties for current selected object
    public float CurrentLocationX 
    {
        get
        {
            if (SelectedCoreObject != null) return SelectedCoreObject.m_location.X;
            if (SelectedCollision != null) return SelectedCollision.X;
            return 0f;
        }
    }
    
    public float CurrentLocationY 
    {
        get
        {
            if (SelectedCoreObject != null) return SelectedCoreObject.m_location.Y;
            if (SelectedCollision != null) return SelectedCollision.Y;
            return 0f;
        }
    }
    
    public float CurrentLocationZ 
    {
        get
        {
            if (SelectedCoreObject != null) return SelectedCoreObject.m_location.Z;
            if (SelectedCollision != null) return SelectedCollision.Z;
            return 0f;
        }
    }
    
    public float CurrentScale 
    {
        get
        {
            if (SelectedCoreObject != null) return SelectedCoreObject.m_fScale;
            if (SelectedCollision != null) return SelectedCollision.Scale;
            return 1f;
        }
    }

    public ICommand LoadZoneCommand { get; }
    public ICommand SaveZoneCommand { get; }
    public ICommand SelectObjectCommand { get; }
    public ICommand SelectCollisionCommand { get; }
    public ICommand SelectPathCommand { get; }
    public ICommand SelectMeshCommand { get; }
    public ICommand SelectVolumeCommand { get; }
    public ICommand SelectTriggerCommand { get; }
    public ICommand SelectZoneCommand { get; }
    public ICommand EditNpcInventoryCommand { get; }
    public ICommand EditNpcSpellInventoryCommand { get; }
    public ICommand EditCreatureDeckCommand { get; }
    public ICommand ViewDropTableCommand { get; }
    public ICommand CreateDropTableCommand { get; }
    public ICommand OpenOrCreateDropTableCommand { get; }
    
    // Viewport commands
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetViewCommand { get; }
    public ICommand PanUpCommand { get; }
    public ICommand PanDownCommand { get; }
    public ICommand PanLeftCommand { get; }
    public ICommand PanRightCommand { get; }

    // Export command
    public ICommand ExportSelectedAsJsonCommand { get; }

    // Teleport database commands
    public ICommand EditTeleportCommand { get; }
    public ICommand CreateTeleportCommand { get; }
    
    // Node details for Properties panel
    public string SelectedNodeName => SelectedNode != null ? $"Node {SelectedNode.NodeIndex}" : "No Node Selected";
    public string SelectedNodeId => SelectedNode != null ? $"Node ID: {SelectedNode.NodeId}" : string.Empty;
    public string SelectedNodePath => SelectedNode != null && SelectedNode.PathId != 0 ? $"Path ID: {SelectedNode.PathId}" : "Unlinked";
    public string SelectedNodeIndex => SelectedNode != null ? $"Index: {SelectedNode.NodeIndex}" : string.Empty;
    public string SelectedNodeLocation => SelectedNode != null ? $"Position: ({SelectedNode.X:F1}, {SelectedNode.Y:F1}, {SelectedNode.Z:F1})" : string.Empty;

    public List<NodePropertyItem> SelectedNodeAllProperties
    {
        get
        {
            var list = new List<NodePropertyItem>();
            var obj = SelectedNode?.OriginalNodeObject;
            if (obj == null) return list;
            try
            {
                var type = obj.GetType();
                list.Add(new NodePropertyItem { Name = "Type", Value = type.Name });
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanRead) continue;
                    var val = prop.GetValue(obj);
                    string str = FormatNodePropertyValue(val);
                    list.Add(new NodePropertyItem { Name = prop.Name, Value = str });
                }
            }
            catch (Exception ex)
            {
                list.Add(new NodePropertyItem { Name = "Error", Value = ex.Message });
            }
            return list;
        }
    }

    private string FormatNodePropertyValue(object? val)
    {
        if (val == null) return "<null>";
        try
        {
            var t = val.GetType();
            var fullName = t.FullName ?? string.Empty;
            
            // Special-case Imcodec.Types.GID to display the Full value
            if (fullName == "Imcodec.Types.GID")
            {
                var fullProp = t.GetProperty("Full", BindingFlags.Public | BindingFlags.Instance);
                if (fullProp != null)
                {
                    var fullVal = fullProp.GetValue(val);
                    return fullVal?.ToString() ?? "<null>";
                }
            }
            
            // Fallback to ToString()
            return val.ToString() ?? "<null>";
        }
        catch
        {
            return val.ToString() ?? "<null>";
        }
    }
    
    public void SetZoneObjectCanvas(Canvas canvas)
    {
        _zoneObjectCanvas = canvas;
        if (_zoneObjectCanvas != null)
        {
            // Ensure visuals exist on the current canvas
            RebuildSceneVisuals();
        }
    }
    
public void SetScrollViewer(ScrollViewer scrollViewer)
    {
        _scrollViewer = scrollViewer;
    }

    public void SetOverlayCanvas(Canvas overlay)
    {
        _overlayCanvas = overlay;
    }

    private void SelectZone()
    {
        LoadZone();
    }

    private async void LoadZone()
    {
        try
        {
            // Create and show zone selection dialog
            var dialogViewModel = new ZoneSelectionDialogViewModel(Zones);
            var selectedZone = await ShowZoneSelectionDialog(dialogViewModel);
            
            if (string.IsNullOrEmpty(selectedZone))
            {
                return; // User cancelled
            }

            IsLoading = true;
            SelectedZone = selectedZone;
            Console.WriteLine($"Loading zone: {SelectedZone}");
            
            MessageService.Info($"Loading zone: {SelectedZone}")
                .WithDuration(TimeSpan.FromSeconds(2))
                .Send();

            var (zoneData, collisionData, sceneFile, spawnData, pathData, nodeData, volumeData, triggerData) = await _zoneDataService.LoadZoneDataAsync(SelectedZone);
            _currentZoneData = zoneData;
            _currentCollisionData = collisionData;
            _currentSceneFile = sceneFile;
            _currentSpawnData = spawnData;
            _currentPathData = pathData;
            _currentNodeData = nodeData;
            _currentVolumeData = volumeData;
            _currentTriggerData = triggerData;
            
            if (_currentZoneData != null)
            {
                await PopulateZoneData(_currentZoneData);
                
                if (_currentCollisionData != null)
                {
                    await PopulateCollisionData(_currentCollisionData);
                }
                
                if (_currentSceneFile != null)
                {
                    await PopulateNifGeometry(_currentSceneFile);
                }
                
                // Process path data if available
                if (_currentSpawnData != null || _currentPathData != null || _currentNodeData != null)
                {
                    await PopulatePathData(_currentSpawnData, _currentPathData, _currentNodeData);
                }
                
                // Process volume data if available
                if (_currentVolumeData != null)
                {
                    await PopulateVolumeData(_currentVolumeData);
                }
                
                // Process trigger data if available
                if (_currentTriggerData != null)
                {
                    await PopulateTriggerData(_currentTriggerData);
                }
                
                MessageService.Info($"Zone '{SelectedZone}' loaded successfully")
                    .WithDuration(TimeSpan.FromSeconds(2))
                    .Send();
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Failed to load zone: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
            Console.WriteLine($"Error loading zone: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async System.Threading.Tasks.Task<string?> ShowZoneSelectionDialog(ZoneSelectionDialogViewModel dialogViewModel)
    {
        // Get the main window from the application lifetime
        var appLifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (appLifetime?.MainWindow != null)
        {
            return await Views.ZoneSelectionDialog.ShowDialogAsync(appLifetime.MainWindow, dialogViewModel);
        }
        
        // Fallback - this shouldn't happen in normal operation
        MessageService.Error("Cannot show zone selection dialog - main window not available")
            .WithDuration(TimeSpan.FromSeconds(3))
            .Send();
        return null;
    }

    private async System.Threading.Tasks.Task CheckWorldDatabaseAvailabilityAsync()
    {
        try
        {
            IsWorldDatabaseReady = await DataPack.IsLoadedAsync();
            WorldDatabaseStatusText = IsWorldDatabaseReady ? string.Empty : "No data pack folder is set.";
        }
        catch
        {
            IsWorldDatabaseReady = false;
            WorldDatabaseStatusText = "Data pack could not be loaded.";
        }
    }

    private void SaveZone()
    {
        if (string.IsNullOrEmpty(SelectedZone))
        {
            MessageService.Info("No zone loaded to save.")
                .WithDuration(TimeSpan.FromSeconds(3))
                .Send();
            return;
        }

        MessageService.Info($"Zone saving functionality coming soon...")
            .WithDuration(TimeSpan.FromSeconds(3))
            .Send();
    }

    private async void LoadAvailableZones()
    {
        try
        {
            var zones = await _zoneDataService.GetAvailableZonesAsync();
            Zones.Clear();
            foreach (var zone in zones)
            {
                Zones.Add(zone);
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Failed to load available zones: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
        }
    }

    private async void EditNpcInventory()
    {
        try
        {
            if (SelectedObject == null || SelectedCoreObject == null)
            {
                MessageService.Error("No NPC selected. Please select an NPC object first.").Send();
                return;
            }

            if (!IsSelectedObjectNpc)
            {
                MessageService.Error("Selected object is not an NPC. Please select an object with the NPC flag.").Send();
                return;
            }

            if (!await DataPack.IsLoadedAsync())
            {
                MessageService.Error("Data pack not available. Please ensure a data pack folder is configured and accessible.").Send();
                return;
            }

            var templateId = (ulong)SelectedCoreObject.m_templateID;
            var npcName = SelectedCoreObject.m_zoneTag.OrIfEmpty($"NPC_{templateId}");

            // Load existing inventory data
            MessageService.Info("Loading NPC inventory data...").Send();
            var existingInventory = await NpcInventoryService.GetNpcInventoryAsync(templateId);

            // Create and show the inventory editor
            var editor = new Controls.NpcInventoryEditor(templateId, npcName, existingInventory);
            
            // Find the main window as owner
            var mainWindow = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow 
                : null;
                
            if (mainWindow != null)
            {
                await editor.ShowDialog(mainWindow);
            }
            else
            {
                // Fallback - show as regular window if no main window available
                editor.Show();
            }
            
            // Show success message if saved
            if (editor.WasSaved)
            {
                MessageService.Info($"NPC inventory for {npcName} has been updated in the data pack.").Send();
                
                // Update shopkeeper flag for this NPC
                await UpdateShopkeeperFlagForNpc(templateId);
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Failed to open NPC inventory editor: {ex.Message}").Send();
        }
    }

    private async void EditNpcSpellInventory()
    {
        try
        {
            if (SelectedObject == null || SelectedCoreObject == null)
            {
                MessageService.Error("No NPC selected. Please select an NPC object first.").Send();
                return;
            }

            if (!IsSelectedObjectNpc)
            {
                MessageService.Error("Selected object is not an NPC. Please select an object with the NPC flag.").Send();
                return;
            }

            if (!await DataPack.IsLoadedAsync())
            {
                MessageService.Error("Data pack not available. Please ensure a data pack folder is configured and accessible.").Send();
                return;
            }

            var templateId = (ulong)SelectedCoreObject.m_templateID;
            var npcName = SelectedCoreObject.m_zoneTag.OrIfEmpty($"NPC_{templateId}");

            // Load existing spell inventory data
            MessageService.Info("Loading NPC spell inventory data...").Send();
            var existingSpellInventory = await NpcSpellInventoryService.GetNpcSpellInventoryAsync(templateId);

            // Create and show the spell inventory editor
            var editor = new Controls.NpcSpellInventoryEditor(templateId, npcName, existingSpellInventory);
            
            // Find the main window as owner
            var mainWindow = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow 
                : null;
                
            if (mainWindow != null)
            {
                await editor.ShowDialog(mainWindow);
            }
            else
            {
                // Fallback - show as regular window if no main window available
                editor.Show();
            }
            
            // Show success message if saved
            if (editor.WasSaved)
            {
                MessageService.Info($"NPC spell inventory for {npcName} has been updated in the data pack.").Send();
                
                // Update professor flag for this NPC
                await UpdateProfessorFlagForNpc(templateId);
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Failed to open NPC spell inventory editor: {ex.Message}").Send();
        }
    }

    private async Task EditCreatureDeck()
    {
        try
        {
            var deckName = SelectedSpawnDeckName;
            if (string.IsNullOrEmpty(deckName))
            {
                MessageService.Error("No creature deck detected. Please select a spawn with both NPCBehaviorTemplate and DuelistBehaviorTemplate.").Send();
                return;
            }

            if (!await DataPack.IsLoadedAsync())
            {
                MessageService.Error("Data pack not available. Please ensure a data pack folder is configured and accessible.").Send();
                return;
            }

            var creatureName = SelectedSpawn?.Name ?? "Unknown Creature";

            // Load existing deck data
            MessageService.Info("Loading creature deck data...").Send();
            var existingDeck = await CreatureDeckService.GetCreatureDeckAsync(deckName);
            
            if (existingDeck != null)
            {
                Console.WriteLine($"[DECK DEBUG] Loaded existing deck '{existingDeck.DeckName}' with {existingDeck.SpellTemplateIds.Count} spells: [{string.Join(", ", existingDeck.SpellTemplateIds)}]");
            }
            else
            {
                Console.WriteLine($"[DECK DEBUG] No existing deck found for '{deckName}' - will create new deck");
            }

            // Create and show the deck editor
            var editor = new Controls.CreatureDeckEditor(deckName, creatureName, existingDeck);
            
            // Find the main window as owner
            var mainWindow = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow 
                : null;
                
            if (mainWindow != null)
            {
                await editor.ShowDialog(mainWindow);
            }
            else
            {
                // Fallback - show as regular window if no main window available
                editor.Show();
            }
            
            // Show success message if saved
            if (editor.WasSaved)
            {
                MessageService.Info($"Creature deck '{deckName}' has been updated in the data pack.").Send();
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Failed to open creature deck editor: {ex.Message}").Send();
        }
    }

    private async Task PopulateZoneData(WizZoneData zoneData)
    {
        // Process zone data on background thread, but update UI collections on UI thread
        var tempZoneObjects = new List<ZoneObjectItem>();
        var tempShopkeepers = new List<ShopkeeperItem>();
        var tempZoneTransfers = new List<ZoneTransferItem>();
        var tempVisualizationObjects = new List<ZoneVisualizationObject>();
        
        await Task.Run(() =>
        {
            Console.WriteLine($"PopulateZoneData called with zone: {zoneData?.m_zoneName}");
            Console.WriteLine($"Object list is null: {zoneData?.m_objectList == null}");
            Console.WriteLine($"Object list count: {zoneData?.m_objectList?.Count ?? 0}");

            // Process zone objects
            if (zoneData.m_objectList != null)
            {
                Console.WriteLine($"Processing {zoneData.m_objectList.Count} objects from zone data");
                var validObjects = zoneData.m_objectList.Where(o => o != null).ToList();
                Console.WriteLine($"Found {validObjects.Count} non-null objects");
                
                foreach (var obj in validObjects)
                {
                    // Load template for this object to detect flags
                    GameObjectTemplate? template = null;
                    try
                    {
                        // Load template synchronously (we're already in a background task)
                        var templateTask = LoadTemplateAsync(obj);
                        template = templateTask.GetAwaiter().GetResult() as GameObjectTemplate;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load template for object {obj.m_zoneTag}: {ex.Message}");
                    }

                    // Detect flags for this object
                    var flags = ObjectFlagService.DetectFlags(template, obj);
                    
                    // Add to general objects list
                    var zoneObjectItem = new ZoneObjectItem
                    {
                        Name = obj.m_zoneTag.OrIfEmpty($"Object_{obj.m_templateID}"),
                        Type = DetermineObjectType(obj),
                        TemplateID = (ulong)obj.m_templateID,
                        Location = obj.m_location,
                        Scale = obj.m_fScale
                    };
                    tempZoneObjects.Add(zoneObjectItem);

                    // Add to visualization with flags
                    var visualObj = new ZoneVisualizationObject
                    {
                        Name = zoneObjectItem.Name,
                        Type = zoneObjectItem.Type,
                        X = obj.m_location.X,
                        Y = obj.m_location.Y,
                        Z = obj.m_location.Z,
                        Scale = obj.m_fScale,
                        TemplateID = (ulong)obj.m_templateID,
                        Flags = flags
                    };
                    
                    // Debug: Log object coordinates and flags
                    if (tempVisualizationObjects.Count < 10) // Only log first 10 objects to avoid spam
                    {
                        var flagsText = flags.Count > 0 ? $" [Flags: {string.Join(", ", flags.Select(f => f.Letter))}]" : "";
                        Console.WriteLine($"Object: {visualObj.Name} at ({visualObj.X:F1}, {visualObj.Y:F1}, {visualObj.Z:F1}) - Type: {visualObj.Type}{flagsText}");
                    }
                    
                    tempVisualizationObjects.Add(visualObj);
                }
            }

            // Process teleports (mock data for now)
            tempZoneTransfers.Add(new ZoneTransferItem { TriggerName = "ZoneExit1", DestinationZone = "Commons" });
            tempZoneTransfers.Add(new ZoneTransferItem { TriggerName = "ZoneExit2", DestinationZone = "Ravenwood" });
            
            Console.WriteLine($"Zone processing complete. Temp collection counts:");
            Console.WriteLine($"  tempVisualizationObjects: {tempVisualizationObjects.Count}");
            Console.WriteLine($"  tempZoneObjects: {tempZoneObjects.Count}");
            Console.WriteLine($"  tempShopkeepers: {tempShopkeepers.Count}");
        });
        
        // Now update the UI collections on the UI thread
        Console.WriteLine($"Updating UI collections on UI thread...");
        
        // Clear existing data
        ZoneObjects.Clear();
        Shopkeepers.Clear(); // Will be populated by database-driven logic
        Professors.Clear(); // Will be populated by database-driven logic
        ZoneTransfers.Clear();
        ZoneVisualizationObjects.Clear();
        CollisionVisualizationObjects.Clear();
        NifMeshVisualizationObjects.Clear();
        SpawnVisualizationObjects.Clear();
        PathVisualizationObjects.Clear();
        NodeVisualizationObjects.Clear();
        VolumeVisualizationObjects.Clear();
        TriggerVisualizationObjects.Clear();
        
        // Add all items to UI collections
        foreach (var item in tempZoneObjects)
            ZoneObjects.Add(item);
            
        foreach (var item in tempZoneTransfers)
            ZoneTransfers.Add(item);
            
        foreach (var item in tempVisualizationObjects)
            ZoneVisualizationObjects.Add(item);
            
        Console.WriteLine($"UI collections updated. Final counts:");
        Console.WriteLine($"  ZoneVisualizationObjects: {ZoneVisualizationObjects.Count}");
        Console.WriteLine($"  ZoneObjects: {ZoneObjects.Count}");
        
        // Now create visual objects on the canvas
        CreateVisualObjects();
        
        // Update shopkeeper flags based on database inventory data
        // This must be done after ZoneVisualizationObjects is populated
        await UpdateShopkeeperFlagsFromDatabase();
        
        // Update professor flags based on database spell inventory data
        await UpdateProfessorFlagsFromDatabase();
        
        // DEBUG: Test the spell inventory system
        await TestSpellInventorySystem();
    }

    private async Task PopulateCollisionData(Bcd collisionData)
    {
        var tempCollisionObjects = new List<CollisionVisualizationObject>();
        
        await Task.Run(() =>
        {
            Console.WriteLine($"PopulateCollisionData called with {collisionData.Collisions.Count} collision objects");
            
            foreach (var collision in collisionData.Collisions)
            {
                var collisionObj = new CollisionVisualizationObject
                {
                    Name = !string.IsNullOrEmpty(collision.Geometry.Name) ? collision.Geometry.Name : $"Collision_{collision.Geometry.Params.TypeId}",
                    GeometryType = GetGeometryTypeName(collision.Geometry.Params.TypeId),
                    X = collision.Geometry.Location[0],
                    Y = collision.Geometry.Location[1],
                    Z = collision.Geometry.Location[2],
                    Scale = collision.Geometry.Scale,
                    Material = collision.Geometry.Material,
                    CategoryFlags = collision.CategoryFlags,
                    CollisionFlags = collision.CollisionFlags,
                    GeometryParams = collision.Geometry.Params,
                    Mesh = collision.Mesh
                };
                
                // Copy rotation matrix
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        collisionObj.Rotation[i, j] = collision.Geometry.Rotation[i, j];
                    }
                }
                
                tempCollisionObjects.Add(collisionObj);
                
                // Debug: Log first few collision objects
                if (tempCollisionObjects.Count <= 5)
                {
                    Console.WriteLine($"Collision: {collisionObj.Name} at ({collisionObj.X:F1}, {collisionObj.Y:F1}, {collisionObj.Z:F1}) - Type: {collisionObj.GeometryType}");
                }
            }
            
            Console.WriteLine($"Collision processing complete. Created {tempCollisionObjects.Count} collision visualization objects");
        });
        
        // Update UI collection on UI thread
        foreach (var item in tempCollisionObjects)
            CollisionVisualizationObjects.Add(item);
            
        Console.WriteLine($"CollisionVisualizationObjects updated. Final count: {CollisionVisualizationObjects.Count}");
        
        // Notify property changes for collision counts
        this.RaisePropertyChanged(nameof(VisibleCollisionCount));
        this.RaisePropertyChanged(nameof(BoxCollisionCount));
        this.RaisePropertyChanged(nameof(SphereCollisionCount));
        this.RaisePropertyChanged(nameof(CylinderCollisionCount));
        this.RaisePropertyChanged(nameof(TubeCollisionCount));
        this.RaisePropertyChanged(nameof(PlaneCollisionCount));
        this.RaisePropertyChanged(nameof(MeshCollisionCount));
        this.RaisePropertyChanged(nameof(RayCollisionCount));
        
        // Create collision visuals now that we have collision data
        CreateCollisionVisuals();
    }

    private async Task PopulateNifGeometry(NifFile sceneFile)
    {
        var tempNifMeshObjects = new List<NifMeshVisualizationObject>();
        
        await Task.Run(() =>
        {
            Console.WriteLine($"PopulateNifGeometry called with NIF file containing {sceneFile.Blocks.Length} blocks");
            
            if (_nifProcessor != null)
            {
                var processedGeometry = _nifProcessor.ProcessNifFile(sceneFile);
                
                Console.WriteLine($"Processed NIF geometry: {processedGeometry.Meshes.Count} meshes, has geometry: {processedGeometry.HasGeometry}");
                
                foreach (var mesh in processedGeometry.Meshes)
                {
                    if (mesh.HasValidGeometry)
                    {
                        var nifMeshObj = new NifMeshVisualizationObject
                        {
                            Name = mesh.Name,
                            Vertices2D = mesh.Vertices2D,
                            Triangles = mesh.Triangles,
                            BoundingBox = mesh.BoundingBox,
                            VertexCount = mesh.Vertices2D.Count,
                            TriangleCount = mesh.Triangles.Count
                        };
                        
                        tempNifMeshObjects.Add(nifMeshObj);
                        
                        // Debug: Log first few mesh objects
                        if (tempNifMeshObjects.Count <= 5)
                        {
                            Console.WriteLine($"NIF Mesh: {nifMeshObj.Name} with {nifMeshObj.VertexCount} vertices and {nifMeshObj.TriangleCount} triangles");
                        }
                    }
                }
            }
            
            Console.WriteLine($"NIF geometry processing complete. Created {tempNifMeshObjects.Count} mesh visualization objects");
        });
        
        // Update UI collection on UI thread
        foreach (var item in tempNifMeshObjects)
            NifMeshVisualizationObjects.Add(item);
            
        Console.WriteLine($"NifMeshVisualizationObjects updated. Final count: {NifMeshVisualizationObjects.Count}");
        
        // Create NIF geometry visuals now that we have mesh data
        CreateNifGeometryVisuals();
    }

    private async Task PopulatePathData(SpawnManager? spawnData, PathTemplateList? pathData, NodeTemplateList? nodeData)
    {
        var tempSpawns = new List<SpawnVisualizationObject>();
        var tempPaths = new List<PathVisualizationObject>();
        var tempNodes = new List<NodeVisualizationObject>();
        
        await Task.Run(() =>
        {
            Console.WriteLine("=== ROBUST PATH DATA PROCESSING ===");
            Console.WriteLine($"Input data status:");
            Console.WriteLine($"  - Spawn data: {(spawnData?.m_spawners != null ? $"{spawnData.m_spawners.Count} spawners" : "null")}");
            Console.WriteLine($"  - Path data: {(pathData?.m_pathList != null ? $"{pathData.m_pathList.Count} paths" : "null")}");
            Console.WriteLine($"  - Node data: {(nodeData?.m_nodeList != null ? $"{nodeData.m_nodeList.Count} nodes" : "null")}");
            
            // STEP 1: Process nodes first since everything references them
            Dictionary<ulong, NodeVisualizationObject> nodeMap = new();
            if (nodeData?.m_nodeList != null && nodeData.m_nodeList.Count > 0)
            {
                Console.WriteLine($"STEP 1: Processing {nodeData.m_nodeList.Count} nodes...");
                
                int validNodes = 0;
                foreach (var nodeObj in nodeData.m_nodeList)
                {
                    if (nodeObj != null)
                    {
                        try
                        {
                            var nodeVis = new NodeVisualizationObject
                            {
                                NodeId = (ulong)nodeObj.m_id,
                                Name = $"Node_{nodeObj.m_id}",
                                X = nodeObj.m_location.X,
                                Y = nodeObj.m_location.Y,
                                Z = nodeObj.m_location.Z,
                                PathId = 0, // Will be set when processing paths
                                NodeIndex = -1, // Will be set when processing paths
                                OriginalNodeObject = nodeObj
                            };
                            
                            tempNodes.Add(nodeVis);
                            nodeMap[(ulong)nodeObj.m_id] = nodeVis;
                            validNodes++;
                            
                            if (validNodes <= 5) // Log first 5 for debugging
                            {
                                Console.WriteLine($"  Node {validNodes}: ID={nodeObj.m_id}, Pos=({nodeObj.m_location.X:F1}, {nodeObj.m_location.Y:F1}, {nodeObj.m_location.Z:F1})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ERROR processing node {nodeObj.m_id}: {ex.Message}");
                        }
                    }
                }
                Console.WriteLine($"STEP 1 COMPLETE: Processed {validNodes} valid nodes out of {nodeData.m_nodeList.Count} total");
            }
            else
            {
                Console.WriteLine("STEP 1 SKIPPED: No valid node data available");
            }
            
            // STEP 2: Process paths and link them to nodes
            Dictionary<ulong, PathVisualizationObject> pathMap = new();
            if (pathData?.m_pathList != null && pathData.m_pathList.Count > 0)
            {
                Console.WriteLine($"STEP 2: Processing {pathData.m_pathList.Count} paths...");
                
                int validPaths = 0;
                int pathsWithNodes = 0;
                foreach (var pathTemplate in pathData.m_pathList)
                {
                    if (pathTemplate != null)
                    {
                        try
                        {
                            var pathVis = new PathVisualizationObject
                            {
                                PathId = (ulong)pathTemplate.m_id,
                                Name = !string.IsNullOrEmpty(pathTemplate.m_name) ? pathTemplate.m_name : $"Path_{pathTemplate.m_id}",
                                PathType = "Creature Path",
                                Nodes = new List<NodeVisualizationObject>(),
                                OriginalPathObject = pathTemplate
                            };
                            
                            // Link nodes to this path
                            if (pathTemplate.m_nodeIDs != null && pathTemplate.m_nodeIDs.Count > 0)
                            {
                                var linkedNodes = new List<NodeVisualizationObject>();
                                for (int i = 0; i < pathTemplate.m_nodeIDs.Count; i++)
                                {
                                    var nodeId = (ulong)pathTemplate.m_nodeIDs[i];
                                    if (nodeMap.TryGetValue(nodeId, out var node))
                                    {
                                        // Update node with path information
                                        node.PathId = pathVis.PathId;
                                        node.NodeIndex = i;
                                        linkedNodes.Add(node);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"  WARNING: Path {pathTemplate.m_id} references missing node {nodeId}");
                                    }
                                }
                                pathVis.Nodes = linkedNodes;
                                
                                if (linkedNodes.Count > 0)
                                {
                                    pathsWithNodes++;
                                    if (validPaths < 3) // Log first few for debugging
                                    {
                                        Console.WriteLine($"  Path {pathTemplate.m_id}: '{pathVis.Name}' with {linkedNodes.Count} nodes");
                                    }
                                }
                            }
                            
                            tempPaths.Add(pathVis);
                            pathMap[(ulong)pathTemplate.m_id] = pathVis;
                            validPaths++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ERROR processing path {pathTemplate.m_id}: {ex.Message}");
                        }
                    }
                }
                Console.WriteLine($"STEP 2 COMPLETE: Processed {validPaths} paths, {pathsWithNodes} have linked nodes");
            }
            else
            {
                Console.WriteLine("STEP 2 SKIPPED: No valid path data available");
            }
            
            // STEP 3: Process spawn data and link to paths
            if (spawnData?.m_spawners != null && spawnData.m_spawners.Count > 0)
            {
                Console.WriteLine($"STEP 3: Processing spawn data from {spawnData.m_spawners.Count} spawners...");
                
                int totalSpawnItems = 0;
                int spawnsWithPaths = 0;
                int spawnsWithValidPaths = 0;
                
                foreach (var spawner in spawnData.m_spawners)
                {
                    if (spawner?.m_spawnList != null)
                    {
                        foreach (var spawnItem in spawner.m_spawnList)
                        {
                            if (spawnItem?.m_objectInfo != null)
                            {
                                totalSpawnItems++;
                                
                                try
                                {
                                    var objInfo = spawnItem.m_objectInfo;
                                    
                                    // Create spawn visualization object
                                    var spawnVis = new SpawnVisualizationObject
                                    {
                                        SpawnId = objInfo.m_nObjectID,
                                        TemplateId = objInfo.m_templateID.Full, // Extract template ID from SpawnObjectInfo
                                        Name = !string.IsNullOrEmpty(objInfo.m_zoneTag) ? objInfo.m_zoneTag : 
                                               !string.IsNullOrEmpty(objInfo.m_overrideName) ? objInfo.m_overrideName : 
                                               $"Spawn_{objInfo.m_nObjectID}",
                                        X = objInfo.m_location.X,
                                        Y = objInfo.m_location.Y,
                                        Z = objInfo.m_location.Z,
                                        PathId = (ulong)objInfo.m_pathID,
                                        CreatureType = "Loading...", // Will be updated when template loads
                                        SpawnChance = spawnItem.m_percentChance
                                    };
                                    
                                    // Check if this spawn has a valid path
                                    if (objInfo.m_pathID != 0)
                                    {
                                        spawnsWithPaths++;
                                        if (pathMap.ContainsKey((ulong)objInfo.m_pathID))
                                        {
                                            spawnsWithValidPaths++;
                                            // Attach this spawn under the path for hierarchy display
                                            pathMap[(ulong)objInfo.m_pathID].Spawns.Add(spawnVis);
                                            if (spawnsWithValidPaths <= 5) // Log first few
                                            {
                                                Console.WriteLine($"  Spawn '{spawnVis.Name}': Pos=({spawnVis.X:F1}, {spawnVis.Y:F1}), PathID={objInfo.m_pathID}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"  WARNING: Spawn '{spawnVis.Name}' references missing path {objInfo.m_pathID}");
                                        }
                                    }
                                    
                                    tempSpawns.Add(spawnVis);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"  ERROR processing spawn item: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                
                Console.WriteLine($"STEP 3 COMPLETE: Processed {totalSpawnItems} spawn items");
                Console.WriteLine($"  - {spawnsWithPaths} have non-zero path IDs");
                Console.WriteLine($"  - {spawnsWithValidPaths} reference valid existing paths");
            }
            else
            {
                Console.WriteLine("STEP 3 SKIPPED: No valid spawn data available");
            }
            
            Console.WriteLine("=== PROCESSING SUMMARY ===");
            Console.WriteLine($"Final counts: {tempSpawns.Count} spawns, {tempPaths.Count} paths, {tempNodes.Count} nodes");
            
            // Validation checks
            if (tempPaths.Count > 0 && tempNodes.Count == 0)
            {
                Console.WriteLine("WARNING: Found paths but no nodes - paths will not be visualizable!");
            }
            if (tempNodes.Count > 0 && tempPaths.Count == 0)
            {
                Console.WriteLine("WARNING: Found nodes but no paths - nodes will appear disconnected!");
            }
            if (tempSpawns.Count == 0 && (tempPaths.Count > 0 || tempNodes.Count > 0))
            {
                Console.WriteLine("INFO: No spawn creatures found, but path/node data exists");
            }
        });
        
        // Update UI collections on UI thread
        Console.WriteLine("Updating UI collections...");
        foreach (var item in tempSpawns)
            SpawnVisualizationObjects.Add(item);
            
        foreach (var item in tempPaths)
            PathVisualizationObjects.Add(item);
            
        foreach (var item in tempNodes)
            NodeVisualizationObjects.Add(item);
            
        Console.WriteLine($"UI collections updated. Final counts: Spawns: {SpawnVisualizationObjects.Count}, Paths: {PathVisualizationObjects.Count}, Nodes: {NodeVisualizationObjects.Count}");
        
        // Create path visuals now that we have all data
        CreatePathVisuals();
        
        // Load templates for spawns asynchronously (don't await to avoid blocking UI)
        if (SpawnVisualizationObjects.Count > 0)
        {
            _ = Task.Run(async () => await LoadSpawnTemplatesAsync());
        }
    }

    private async Task PopulateVolumeData(WizZoneVolumes volumeData)
    {
        var tempVolumeObjects = new List<VolumeVisualizationObject>();
        
        await Task.Run(() =>
        {
            Console.WriteLine($"PopulateVolumeData called with {volumeData.m_volumes?.Count ?? 0} volumes");
            
            if (volumeData.m_volumes != null)
            {
                foreach (var volume in volumeData.m_volumes)
                {
                    if (volume != null)
                    {
                        var volumeVis = new VolumeVisualizationObject(volume);
                        tempVolumeObjects.Add(volumeVis);
                        
                        // Debug: Log first few volume objects
                        if (tempVolumeObjects.Count <= 5)
                        {
                            Console.WriteLine($"Volume: {volumeVis.Name} at ({volumeVis.X:F1}, {volumeVis.Y:F1}, {volumeVis.Z:F1}) - Type: {volumeVis.PrimitiveType}");
                        }
                    }
                }
            }
            
            Console.WriteLine($"Volume processing complete. Created {tempVolumeObjects.Count} volume visualization objects");
        });
        
        // Update UI collection on UI thread
        foreach (var item in tempVolumeObjects)
            VolumeVisualizationObjects.Add(item);
            
        Console.WriteLine($"VolumeVisualizationObjects updated. Final count: {VolumeVisualizationObjects.Count}");
        
        // Create volume visuals now that we have volume data
        CreateVolumeVisuals();
    }

    private async Task PopulateTriggerData(WizZoneTriggers triggerData)
    {
        var tempTriggerObjects = new List<TriggerVisualizationObject>();
        var currentZoneName = _selectedZone; // Capture zone name before entering Task.Run

        await Task.Run(() =>
        {
            Console.WriteLine($"PopulateTriggerData called with {triggerData.m_triggers?.Count ?? 0} triggers");

            if (triggerData.m_triggers != null)
            {
                foreach (var trigger in triggerData.m_triggers)
                {
                    if (trigger != null)
                    {
                        var triggerVis = new TriggerVisualizationObject(trigger, currentZoneName);

                        // Detect and apply flags for the trigger
                        triggerVis.Flags = ObjectFlagService.DetectTriggerFlags(trigger);

                        tempTriggerObjects.Add(triggerVis);

                        // Debug: Log first few trigger objects
                        if (tempTriggerObjects.Count <= 5)
                        {
                            Console.WriteLine($"Trigger: {triggerVis.Name} at ({triggerVis.X:F1}, {triggerVis.Y:F1}, {triggerVis.Z:F1}) - Timing: {triggerVis.TimingText}");
                        }
                    }
                }
            }

            Console.WriteLine($"Trigger processing complete. Created {tempTriggerObjects.Count} trigger visualization objects");
        });
        
        // Update UI collection on UI thread
        foreach (var item in tempTriggerObjects)
            TriggerVisualizationObjects.Add(item);
            
        Console.WriteLine($"TriggerVisualizationObjects updated. Final count: {TriggerVisualizationObjects.Count}");
        
        // Note: Triggers don't have visual representation in the viewport, they only appear in the scene hierarchy
    }

    private string GetGeometryTypeName(uint typeId)
    {
        return typeId switch
        {
            0 => "Box",
            1 => "Ray", 
            2 => "Sphere",
            3 => "Cylinder",
            4 => "Tube",
            5 => "Plane",
            6 => "Mesh",
            _ => $"Unknown({typeId})"
        };
    }

    // Viewport control methods
    private void ZoomIn()
    {
        ZoomLevel += ZoomStep;
    }

    private void ZoomOut()
    {
        ZoomLevel -= ZoomStep;
    }

    private void ResetView()
    {
        ZoomLevel = 0.5; // Reset to 50% zoom
        InitializeViewportCenter();
    }
    
    public void InitializeViewportCenter()
    {
        if (_scrollViewer == null) return;
        
        // Use dispatcher to ensure ScrollViewer is properly initialized
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Center the ScrollViewer on game coordinates (0,0)
            // Game coordinate (0,0) is at canvas position (10000, 10000)
            // Get actual viewport size or use reasonable defaults
            var viewportWidth = _scrollViewer.Viewport.Width > 0 ? _scrollViewer.Viewport.Width : 800;
            var viewportHeight = _scrollViewer.Viewport.Height > 0 ? _scrollViewer.Viewport.Height : 600;
            
            // Center by scrolling to that position minus half the viewport size
            var scrollToX = Math.Max(0, 10000 - (viewportWidth / 2));
            var scrollToY = Math.Max(0, 10000 - (viewportHeight / 2));
            
            Console.WriteLine($"Centering viewport: scrolling to ({scrollToX:F1}, {scrollToY:F1}), viewport size: ({viewportWidth:F1}, {viewportHeight:F1})");
            _scrollViewer.Offset = new Vector(scrollToX, scrollToY);
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }
    
private void UpdateVisualSelection()
    {
        if (_zoneObjectCanvas == null)
            return;
        
        try
        {
            // 1) Highlight Zone Objects (small markers)
            foreach (var child in _zoneObjectCanvas.Children.OfType<Border>())
            {
                if (child.Tag is ZoneVisualizationObject obj)
                {
                    bool isSelected = SelectedObject != null && obj.TemplateID == SelectedObject.TemplateID;
                    ApplyBorderHighlight(child, isSelected, defaultThickness: 1, defaultBrush: Brushes.White);
                    if (isSelected) child.IsVisible = true;
                }
                else if (child.Tag is SpawnVisualizationObject spawn)
                {
                    bool isSelected = SelectedSpawn != null && spawn.SpawnId == SelectedSpawn.SpawnId;
                    // Spawn visuals default border thickness is 2 and Brush is White
                    ApplyBorderHighlight(child, isSelected, defaultThickness: 2, defaultBrush: Brushes.White);
                    if (isSelected) child.IsVisible = true;
                }
            }
            
            // 2) Highlight Nodes
            foreach (var shape in _zoneObjectCanvas.Children.OfType<Shape>())
            {
                if (shape.Tag is NodeVisualizationObject node)
                {
                    bool isSelectedNode = SelectedNode != null && node.NodeId == SelectedNode.NodeId;
                    bool isInSelectedPath = SelectedPath != null && node.PathId == SelectedPath.PathId;
                    bool highlight = isSelectedNode || isInSelectedPath;
                    ApplyShapeHighlight(shape, highlight, defaultThickness: 1, defaultBrush: new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 255)));
                    if (highlight) shape.IsVisible = true;
                }
                else if (shape.Tag is CollisionVisualizationObject collision)
                {
bool isSelected = SelectedCollision != null && ReferenceEquals(collision, SelectedCollision);
                    // Collisions default stroke is cyan-ish with thickness 1/2 depending on shape; use 1 as base
                    ApplyShapeHighlight(shape, isSelected, defaultThickness: 1, defaultBrush: new SolidColorBrush(Avalonia.Media.Color.FromRgb(0, 200, 255)));
                    if (isSelected) shape.IsVisible = true;
                }
                else if (shape.Tag is NifMeshVisualizationObject mesh)
                {
                    bool isSelected = SelectedMesh != null && ReferenceEquals(mesh, SelectedMesh);
                    // NIF default stroke is yellow
                    ApplyShapeHighlight(shape, isSelected, defaultThickness: 1, defaultBrush: new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 0)));
                    if (isSelected) shape.IsVisible = true;
                }
                else if (shape.Tag is PathLineTag pathTag)
                {
                    bool isSelected = SelectedPath != null && pathTag.Path != null && pathTag.Path.PathId == SelectedPath.PathId;
                    if (isSelected)
                    {
                        shape.Stroke = HighlightBrush;
                        shape.StrokeThickness = HighlightThickness;
                        shape.IsVisible = true;
                    }
                    else
                    {
                        // Reset to uniform base style for all non-selected paths
                        shape.Stroke = PathBaseBrush;
                        shape.StrokeThickness = 2;
                    }
                }
                else if (shape.Tag is VolumeVisualizationObject volumeTag)
                {
                    bool isSelected = SelectedVolume != null && ReferenceEquals(volumeTag, SelectedVolume);
                    // Volume visuals often are children of a Canvas, but some shapes may carry the tag
                    ApplyShapeHighlight(shape, isSelected, defaultThickness: 2, defaultBrush: new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 0, 0)));
                    if (isSelected) shape.IsVisible = true;
                }
            }
            
            // 3) Highlight Volume composite canvases (their Tag is the VolumeVisualizationObject)
            foreach (var composite in _zoneObjectCanvas.Children.OfType<Canvas>())
            {
                if (composite.Tag is VolumeVisualizationObject vol)
                {
                    bool isSelected = SelectedVolume != null && ReferenceEquals(vol, SelectedVolume);
                    ApplyCompositeCanvasHighlight(composite, isSelected, defaultBrush: new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 0, 0)), defaultThickness: 2);
                    if (isSelected) composite.IsVisible = true;
                }
            }
            
            // 4) Trigger highlight marker (triggers are not rendered otherwise)
            UpdateTriggerHighlightMarker();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Failed to update visual selection: {ex.Message}");
        }
    }

    private void ApplyBorderHighlight(Border border, bool highlight, double defaultThickness, IBrush defaultBrush)
    {
        if (highlight)
        {
            border.BorderBrush = HighlightBrush;
            border.BorderThickness = new Avalonia.Thickness(HighlightThickness);
        }
        else
        {
            border.BorderBrush = defaultBrush;
            border.BorderThickness = new Avalonia.Thickness(defaultThickness);
        }
    }

    private void ApplyShapeHighlight(Shape shape, bool highlight, double defaultThickness, IBrush defaultBrush)
    {
        if (highlight)
        {
            shape.Stroke = HighlightBrush;
            shape.StrokeThickness = HighlightThickness;
            shape.Opacity = 1.0;
        }
        else
        {
            shape.Stroke = defaultBrush;
            shape.StrokeThickness = defaultThickness;
            // leave opacity as-is for base visuals
        }
    }

    private void ApplyCompositeCanvasHighlight(Canvas canvas, bool highlight, IBrush defaultBrush, double defaultThickness)
    {
        foreach (var child in canvas.Children.OfType<Shape>())
        {
            ApplyShapeHighlight(child, highlight, defaultThickness, defaultBrush);
        }
    }

    private void UpdateTriggerHighlightMarker()
    {
        if (_zoneObjectCanvas == null)
            return;

        if (SelectedTrigger != null)
        {
            // Convert trigger coordinates to canvas
            var canvasX = 10000.0 + (SelectedTrigger.X * 0.25);
            var canvasY = 10000.0 - (SelectedTrigger.Y * 0.25);
            var radius = 18.0;

            if (_triggerHighlightMarker is not Ellipse ellipse)
            {
                ellipse = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = Brushes.Transparent,
                    Stroke = HighlightBrush,
                    StrokeThickness = 3,
                    Opacity = 0.9,
                    IsHitTestVisible = false,
                    Tag = "TriggerHighlightMarker"
                };
                _triggerHighlightMarker = ellipse;
                _zoneObjectCanvas.Children.Add(ellipse);
            }
            else
            {
                ellipse.Width = radius * 2;
                ellipse.Height = radius * 2;
                ellipse.Stroke = HighlightBrush;
                ellipse.StrokeThickness = 3;
                ellipse.IsVisible = true;
            }

            Canvas.SetLeft(ellipse, canvasX - radius);
            Canvas.SetTop(ellipse, canvasY - radius);
        }
        else
        {
            if (_triggerHighlightMarker != null)
            {
                _triggerHighlightMarker.IsVisible = false;
            }
        }
    }

private Control? _selectionArrow;
    private Control? _selectionPulseRing;
    private Control? _selectionPulseOuter;
    private DispatcherTimer? _pulseTimer;
    private double _pulseT;

    public void UpdateViewportIndicator()
    {
        try
        {
if (_overlayCanvas == null || _scrollViewer == null)
            {
                StopPulse();
                return;
            }

            // Determine target canvas position based on actual visuals; fallback to model coords
            double targetContentX, targetContentY;
            if (!TryGetSelectionCenterFromVisuals(out targetContentX, out targetContentY))
            {
                if (!TryGetSelectedCanvasPosition(out targetContentX, out targetContentY))
                {
                    if (_selectionArrow != null) _selectionArrow.IsVisible = false;
                    HideSelectionPulse();
                    return;
                }
            }

            var zoom = Math.Max(ZoomLevel, 0.0001);

            // Viewport rect in content units
            var leftContent = _scrollViewer.Offset.X;
            var topContent = _scrollViewer.Offset.Y;
            var vpWidthContent = _scrollViewer.Viewport.Width / zoom;
            var vpHeightContent = _scrollViewer.Viewport.Height / zoom;
            var rightContent = leftContent + vpWidthContent;
            var bottomContent = topContent + vpHeightContent;

            // If target is inside viewport (content space), hide arrow and show pulse ring
            bool inView = targetContentX >= leftContent && targetContentX <= rightContent && targetContentY >= topContent && targetContentY <= bottomContent;
            if (inView)
            {
                if (_selectionArrow != null) _selectionArrow.IsVisible = false;
                // Compute overlay point directly from visuals to avoid math drift
                if (TryGetSelectionOverlayCenterFromVisuals(out var ovx, out var ovy))
                {
                    UpdateSelectionPulseOverlay(ovx, ovy);
                }
                else
                {
                    zoom = Math.Max(ZoomLevel, 0.0001);
                    var ovx2 = (targetContentX - leftContent) * zoom;
                    var ovy2 = (targetContentY - topContent) * zoom;
                    UpdateSelectionPulseOverlay(ovx2, ovy2);
                }
                return;
            }
            else
            {
                HideSelectionPulse();
            }

            // Viewport center in content coords
            var cx = leftContent + vpWidthContent / 2.0;
            var cy = topContent + vpHeightContent / 2.0;
            var dx = targetContentX - cx;
            var dy = targetContentY - cy;

            if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6)
            {
                if (_selectionArrow != null) _selectionArrow.IsVisible = false;
                return;
            }

            // Find intersection with viewport rectangle edges along ray from center
            var intersections = new List<(double t, double x, double y)>();
            if (Math.Abs(dx) > 1e-6)
            {
                // Left edge
                var tL = (leftContent - cx) / dx;
                var yL = cy + tL * dy;
                if (tL > 0 && yL >= topContent && yL <= bottomContent) intersections.Add((tL, leftContent, yL));
                // Right edge
                var tR = (rightContent - cx) / dx;
                var yR = cy + tR * dy;
                if (tR > 0 && yR >= topContent && yR <= bottomContent) intersections.Add((tR, rightContent, yR));
            }
            if (Math.Abs(dy) > 1e-6)
            {
                // Top edge
                var tT = (topContent - cy) / dy;
                var xT = cx + tT * dx;
                if (tT > 0 && xT >= leftContent && xT <= rightContent) intersections.Add((tT, xT, topContent));
                // Bottom edge
                var tB = (bottomContent - cy) / dy;
                var xB = cx + tB * dx;
                if (tB > 0 && xB >= leftContent && xB <= rightContent) intersections.Add((tB, xB, bottomContent));
            }

            if (intersections.Count == 0)
            {
                if (_selectionArrow != null) _selectionArrow.IsVisible = false;
                return;
            }

            // Choose the nearest intersection (smallest positive t)
            var hit = intersections.OrderBy(h => h.t).First();
            var hitX = hit.x;
            var hitY = hit.y;

            // Convert to overlay coords (pixels relative to viewport)
            var overlayX = (hitX - leftContent) * zoom;
            var overlayY = (hitY - topContent) * zoom;

            // Create arrow if needed
            if (_selectionArrow is not Polygon arrow)
            {
                arrow = new Polygon
                {
                    Points = new List<Avalonia.Point>
                    {
                        new Avalonia.Point(0, -12), // tip
                        new Avalonia.Point(8, 12),
                        new Avalonia.Point(-8, 12)
                    },
                    Fill = HighlightBrush,
                    Stroke = HighlightBrush,
                    StrokeThickness = 2,
                    IsHitTestVisible = false,
                    Tag = "ViewportSelectionArrow"
                };
                _selectionArrow = arrow;
                _overlayCanvas.Children.Add(arrow);
            }
            else
            {
                arrow = (Polygon)_selectionArrow;
            }

            // Position arrow centered on overlay point
            Canvas.SetLeft(arrow, overlayX);
            Canvas.SetTop(arrow, overlayY);

            // Rotate arrow to face target direction
            var angleRad = Math.Atan2(dy, dx);
            var angleDeg = angleRad * 180.0 / Math.PI;
            arrow.RenderTransform = new RotateTransform(angleDeg);
            arrow.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

            // Nudge arrow slightly inside viewport so it remains visible
            const double margin = 14.0;
            var width = _scrollViewer.Viewport.Width;
            var height = _scrollViewer.Viewport.Height;
            var clampedX = Math.Min(Math.Max(overlayX, margin), width - margin);
            var clampedY = Math.Min(Math.Max(overlayY, margin), height - margin);
            Canvas.SetLeft(arrow, clampedX);
            Canvas.SetTop(arrow, clampedY);

            arrow.IsVisible = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Failed to update viewport indicator: {ex.Message}");
        }
    }

    private void UpdateSelectionPulseOverlay(double overlayX, double overlayY)
    {
        try
        {
            if (_overlayCanvas == null || _scrollViewer == null)
                return;

            // Create or update outer glow ring (white)
            if (_selectionPulseOuter is not Ellipse outer)
            {
                outer = new Ellipse
                {
                    Width = 120,
                    Height = 120,
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(Avalonia.Media.Color.FromArgb(160, 255, 255, 255)),
                    StrokeThickness = 10,
                    IsHitTestVisible = false,
                    Tag = "SelectionPulseOuter",
                    Opacity = 0.6
                };
                _selectionPulseOuter = outer;
                _overlayCanvas.Children.Add(outer);
            }

            // Create or update main pulse ring (gold)
            if (_selectionPulseRing is not Ellipse ring)
            {
                ring = new Ellipse
                {
                    Width = 90,
                    Height = 90,
                    Fill = Brushes.Transparent,
                    Stroke = HighlightBrush,
                    StrokeThickness = 6,
                    IsHitTestVisible = false,
                    Tag = "SelectionPulseRing",
                    Opacity = 0.95
                };
                _selectionPulseRing = ring;
                _overlayCanvas.Children.Add(ring);
            }

            // Ensure correct draw order: outer behind ring
            if (_selectionPulseOuter != null && _selectionPulseRing != null)
            {
                _overlayCanvas.Children.Remove(_selectionPulseOuter);
                _overlayCanvas.Children.Remove(_selectionPulseRing);
                _overlayCanvas.Children.Add(_selectionPulseOuter);
                _overlayCanvas.Children.Add(_selectionPulseRing);
            }

// Position centered (use current Width/Height; Bounds may be 0 before layout)
            var outerW = (_selectionPulseOuter as Ellipse)?.Width ?? 0;
            var outerH = (_selectionPulseOuter as Ellipse)?.Height ?? 0;
            Canvas.SetLeft(_selectionPulseOuter, overlayX - outerW / 2);
            Canvas.SetTop(_selectionPulseOuter, overlayY - outerH / 2);
            _selectionPulseOuter.IsVisible = true;

            var ringW = (_selectionPulseRing as Ellipse)?.Width ?? 0;
            var ringH = (_selectionPulseRing as Ellipse)?.Height ?? 0;
            Canvas.SetLeft(_selectionPulseRing, overlayX - ringW / 2);
            Canvas.SetTop(_selectionPulseRing, overlayY - ringH / 2);
            _selectionPulseRing.IsVisible = true;

            // Start or continue the pulse animation
            StartPulse();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Failed to update selection pulse: {ex.Message}");
        }
    }

    private void HideSelectionPulse()
    {
        if (_selectionPulseRing != null) _selectionPulseRing.IsVisible = false;
        if (_selectionPulseOuter != null) _selectionPulseOuter.IsVisible = false;
        StopPulse();
    }

    private void StartPulse()
    {
        if (_pulseTimer != null && _pulseTimer.IsEnabled)
            return;

        _pulseT = 0;
        _pulseTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // ~30 FPS
        _pulseTimer.Tick -= OnPulseTick;
        _pulseTimer.Tick += OnPulseTick;
        _pulseTimer.IsEnabled = true;
    }

    private void StopPulse()
    {
        if (_pulseTimer != null)
        {
            _pulseTimer.IsEnabled = false;
            _pulseTimer.Tick -= OnPulseTick;
        }
    }

    private void OnPulseTick(object? sender, EventArgs e)
    {
        if (_overlayCanvas == null || _scrollViewer == null)
            return;

        // Animate sizes and opacity
        _pulseT += _pulseTimer!.Interval.TotalSeconds / 2.0; // slower pulse: 2.0s period
        if (_pulseT > 1) _pulseT -= 1;
        var s = (1 - Math.Cos(2 * Math.PI * _pulseT)) / 2.0; // 0..1

        if (_selectionPulseRing is Ellipse ring)
        {
            var radius = 40.0 + 50.0 * s; // toned down: 40..90 px
            ring.Width = radius;
            ring.Height = radius;
            ring.StrokeThickness = 4 + 4 * (1 - s);
            ring.Opacity = 0.85;
        }
        if (_selectionPulseOuter is Ellipse outer)
        {
            var radius = 60.0 + 60.0 * s; // toned down: 60..120 px
            outer.Width = radius;
            outer.Height = radius;
            outer.StrokeThickness = 8 + 6 * s;
            outer.Opacity = 0.45 + 0.25 * (1 - s);
        }

        // Keep centered on the current selection and hide when off-screen
        if (TryGetSelectionOverlayCenterFromVisuals(out var overlayX, out var overlayY))
        {
            // Keep ring centered on overlay coords
            if (_selectionPulseRing is Ellipse r)
            {
                Canvas.SetLeft(r, overlayX - r.Width / 2);
                Canvas.SetTop(r, overlayY - r.Height / 2);
            }
            if (_selectionPulseOuter is Ellipse o)
            {
                Canvas.SetLeft(o, overlayX - o.Width / 2);
                Canvas.SetTop(o, overlayY - o.Height / 2);
            }

            // In-view test in overlay space
            var vpWpx = _scrollViewer.Viewport.Width;
            var vpHpx = _scrollViewer.Viewport.Height;
            var inView = overlayX >= 0 && overlayX <= vpWpx && overlayY >= 0 && overlayY <= vpHpx;
            if (_selectionPulseRing is Ellipse r2) r2.IsVisible = inView;
            if (_selectionPulseOuter is Ellipse o2) o2.IsVisible = inView;
        }
        else if (TryGetSelectionCenterFromVisuals(out var cxContent, out var cyContent) || TryGetSelectedCanvasPosition(out cxContent, out cyContent))
        {
            var zoom = Math.Max(ZoomLevel, 0.0001);
            var left = _scrollViewer.Offset.X;
            var top = _scrollViewer.Offset.Y;
            var ovx = (cxContent - left) * zoom;
            var ovy = (cyContent - top) * zoom;
            if (_selectionPulseRing is Ellipse r)
            {
                Canvas.SetLeft(r, ovx - r.Width / 2);
                Canvas.SetTop(r, ovy - r.Height / 2);
            }
            if (_selectionPulseOuter is Ellipse o)
            {
                Canvas.SetLeft(o, ovx - o.Width / 2);
                Canvas.SetTop(o, ovy - o.Height / 2);
            }
        }
    }

    private void GetSelectionCanvasAnchorOffset(out double offXContent, out double offYContent)
    {
        // Default to centered anchors
        offXContent = 0; offYContent = 0;

        // Zone objects are drawn as 16x16 Border at top-left => add half-size
        if (SelectedObject != null)
        {
            offXContent = 8; offYContent = 8;
            return;
        }
        // Spawns are centered in CreateEnhancedSpawnVisual
        if (SelectedSpawn != null)
        {
            offXContent = 0; offYContent = 0; return;
        }
        // Nodes are centered
        if (SelectedNode != null)
        {
            offXContent = 0; offYContent = 0; return;
        }
        // NIF mesh uses centroid of projected vertices
        if (SelectedMesh != null)
        {
            offXContent = 0; offYContent = 0; return;
        }
        // Triggers: just use point
        if (SelectedTrigger != null)
        {
            offXContent = 0; offYContent = 0; return;
        }
        // Volumes: visuals are placed at top-left, sizes depend on primitive
        if (SelectedVolume != null)
        {
            var type = SelectedVolume.PrimitiveType?.ToLowerInvariant() ?? string.Empty;
            if (type == "box" || type == "cube")
            {
                var w = Math.Max(10, SelectedVolume.Width * 0.25);
                var h = Math.Max(10, SelectedVolume.Length * 0.25);
                offXContent = w / 2.0; offYContent = h / 2.0; return;
            }
            if (type == "sphere" || type == "ball" || type == "cylinder")
            {
                var d = Math.Max(10, SelectedVolume.Radius * 2 * 0.25);
                offXContent = d / 2.0; offYContent = d / 2.0; return;
            }
            // Fallback
            offXContent = 0; offYContent = 0; return;
        }
        // Collisions: approximate using geometry
        if (SelectedCollision != null)
        {
            // Use underlying geometry params when available
            if (SelectedCollision.GeometryParams is BcdGeomParams.BoxGeomParams box)
            {
                var w = Math.Max(4, box.Length * SelectedCollision.Scale * 0.25);
                var h = Math.Max(4, box.Width * SelectedCollision.Scale * 0.25);
                offXContent = w / 2.0; offYContent = h / 2.0; return;
            }
            if (SelectedCollision.GeometryParams is BcdGeomParams.SphereGeomParams s)
            {
                var d = Math.Max(4, s.Radius * 2 * SelectedCollision.Scale * 0.25);
                offXContent = d / 2.0; offYContent = d / 2.0; return;
            }
            if (SelectedCollision.GeometryParams is BcdGeomParams.CylinderGeomParams c)
            {
                var d = Math.Max(4, c.Radius * 2 * SelectedCollision.Scale * 0.25);
                offXContent = d / 2.0; offYContent = d / 2.0; return;
            }
            if (SelectedCollision.GeometryParams is BcdGeomParams.TubeGeomParams t)
            {
                var d = Math.Max(4, t.Radius * 2 * SelectedCollision.Scale * 0.25);
                offXContent = d / 2.0; offYContent = d / 2.0; return;
            }
            // For Plane and Ray we can't easily center without orientation; skip
            offXContent = 0; offYContent = 0; return;
        }
    }

    private bool TryGetSelectionOverlayCenterFromVisuals(out double overlayX, out double overlayY)
    {
        overlayX = overlayY = 0;
        if (_zoneObjectCanvas == null || _overlayCanvas == null)
            return false;

        var points = new List<Avalonia.Point>();
        foreach (var child in _zoneObjectCanvas.Children.OfType<Control>())
        {
            if (child.Tag == null) continue;

            bool match = false;
            if (SelectedObject != null && child.Tag is ZoneVisualizationObject z && ReferenceEquals(z, SelectedObject)) match = true;
            else if (SelectedSpawn != null && child.Tag is SpawnVisualizationObject s && ReferenceEquals(s, SelectedSpawn)) match = true;
            else if (SelectedNode != null && child.Tag is NodeVisualizationObject n && ReferenceEquals(n, SelectedNode)) match = true;
            else if (SelectedVolume != null && child.Tag is VolumeVisualizationObject v && ReferenceEquals(v, SelectedVolume)) match = true;
            else if (SelectedCollision != null && child.Tag is CollisionVisualizationObject c && ReferenceEquals(c, SelectedCollision)) match = true;
            else if (SelectedMesh != null && child.Tag is NifMeshVisualizationObject m && ReferenceEquals(m, SelectedMesh)) match = true;
            else if (SelectedPath != null && child.Tag is PathLineTag p && p.Path != null && p.Path.PathId == SelectedPath.PathId) match = true;

            if (!match) continue;

            Avalonia.Point? localCenter = null;
            if (child is Avalonia.Controls.Shapes.Line line)
            {
                localCenter = new Avalonia.Point((line.StartPoint.X + line.EndPoint.X) / 2.0, (line.StartPoint.Y + line.EndPoint.Y) / 2.0);
            }
            else if (child is Polygon poly && poly.Points != null && poly.Points.Count > 0)
            {
                localCenter = new Avalonia.Point(poly.Points.Average(p => p.X), poly.Points.Average(p => p.Y));
            }
            else if (child is Polyline polyline && polyline.Points != null && polyline.Points.Count > 0)
            {
                localCenter = new Avalonia.Point(polyline.Points.Average(p => p.X), polyline.Points.Average(p => p.Y));
            }
            else
            {
                var w = child.Bounds.Width; var h = child.Bounds.Height;
                localCenter = new Avalonia.Point(w > 0 ? w / 2.0 : 0, h > 0 ? h / 2.0 : 0);
            }

            if (localCenter.HasValue)
            {
                var translated = child.TranslatePoint(localCenter.Value, _overlayCanvas);
                if (translated.HasValue)
                    points.Add(translated.Value);
            }
        }

        if (points.Count == 0) { return false; }
        overlayX = points.Average(p => p.X);
        overlayY = points.Average(p => p.Y);
        return true;
    }

    private bool TryGetSelectionCenterFromVisuals(out double x, out double y)
    {
        x = y = 0;
        if (_zoneObjectCanvas == null)
            return false;

        var centers = new List<(double X, double Y)>();
        foreach (var child in _zoneObjectCanvas.Children.OfType<Control>())
        {
            if (child.Tag == null) continue;

            bool match = false;
            if (SelectedObject != null && child.Tag is ZoneVisualizationObject z && ReferenceEquals(z, SelectedObject)) match = true;
            else if (SelectedSpawn != null && child.Tag is SpawnVisualizationObject s && ReferenceEquals(s, SelectedSpawn)) match = true;
            else if (SelectedNode != null && child.Tag is NodeVisualizationObject n && ReferenceEquals(n, SelectedNode)) match = true;
            else if (SelectedVolume != null && child.Tag is VolumeVisualizationObject v && ReferenceEquals(v, SelectedVolume)) match = true;
            else if (SelectedCollision != null && child.Tag is CollisionVisualizationObject c && ReferenceEquals(c, SelectedCollision)) match = true;
            else if (SelectedMesh != null && child.Tag is NifMeshVisualizationObject m && ReferenceEquals(m, SelectedMesh)) match = true;
            else if (SelectedPath != null && child.Tag is PathLineTag p && p.Path != null && p.Path.PathId == SelectedPath.PathId) match = true;

            if (!match) continue;

            // Lines (path segments)
            if (child is Avalonia.Controls.Shapes.Line line)
            {
                var baseL = Canvas.GetLeft(line);
                var baseT = Canvas.GetTop(line);
                if (double.IsNaN(baseL)) baseL = 0;
                if (double.IsNaN(baseT)) baseT = 0;
                var cx = baseL + (line.StartPoint.X + line.EndPoint.X) / 2.0;
                var cy = baseT + (line.StartPoint.Y + line.EndPoint.Y) / 2.0;
                centers.Add((cx, cy));
                continue;
            }
            // Polygons (arrows, meshes)
            if (child is Polygon poly && poly.Points != null && poly.Points.Count > 0)
            {
                var baseL = Canvas.GetLeft(poly); if (double.IsNaN(baseL)) baseL = 0;
                var baseT = Canvas.GetTop(poly); if (double.IsNaN(baseT)) baseT = 0;
                var ax = poly.Points.Average(p => p.X);
                var ay = poly.Points.Average(p => p.Y);
                centers.Add((baseL + ax, baseT + ay));
                continue;
            }
            if (child is Polyline polyline && polyline.Points != null && polyline.Points.Count > 0)
            {
                var baseL = Canvas.GetLeft(polyline); if (double.IsNaN(baseL)) baseL = 0;
                var baseT = Canvas.GetTop(polyline); if (double.IsNaN(baseT)) baseT = 0;
                var ax = polyline.Points.Average(p => p.X);
                var ay = polyline.Points.Average(p => p.Y);
                centers.Add((baseL + ax, baseT + ay));
                continue;
            }
            
            // General case: center from left/top and size, with type-aware width/height fallback
            double baseLeft = Canvas.GetLeft(child); if (double.IsNaN(baseLeft)) baseLeft = 0;
            double baseTop = Canvas.GetTop(child); if (double.IsNaN(baseTop)) baseTop = 0;

            double width = child.Bounds.Width;
            double height = child.Bounds.Height;

            if (width <= 0 || height <= 0)
            {
                switch (child)
                {
                    case Border b:
                        width = b.Width > 0 ? b.Width : width;
                        height = b.Height > 0 ? b.Height : height;
                        break;
                    case Ellipse e:
                        width = e.Width > 0 ? e.Width : width;
                        height = e.Height > 0 ? e.Height : height;
                        break;
                    case Avalonia.Controls.Shapes.Rectangle r:
                        width = r.Width > 0 ? r.Width : width;
                        height = r.Height > 0 ? r.Height : height;
                        break;
                    case Canvas canv:
                        width = canv.Width > 0 ? canv.Width : width;
                        height = canv.Height > 0 ? canv.Height : height;
                        break;
                }
            }

            centers.Add((baseLeft + (width > 0 ? width / 2.0 : 0), baseTop + (height > 0 ? height / 2.0 : 0)));
        }

        if (centers.Count == 0) return false;
        x = centers.Average(c => c.X);
        y = centers.Average(c => c.Y);
        return true;
    }

    private bool TryGetSelectedCanvasPosition(out double x, out double y)
    {
        x = y = 0;

        // Priority order: explicit selected visuals
        if (SelectedObject != null)
        {
            x = 10000.0 + (SelectedObject.X * 0.25);
            y = 10000.0 - (SelectedObject.Y * 0.25);
            return true;
        }
        if (SelectedSpawn != null)
        {
            x = 10000.0 + (SelectedSpawn.X * 0.25);
            y = 10000.0 - (SelectedSpawn.Y * 0.25);
            return true;
        }
        if (SelectedVolume != null)
        {
            x = 10000.0 + (SelectedVolume.X * 0.25);
            y = 10000.0 - (SelectedVolume.Y * 0.25);
            return true;
        }
        if (SelectedCollision != null)
        {
            x = 10000.0 + (SelectedCollision.X * 0.25);
            y = 10000.0 - (SelectedCollision.Y * 0.25);
            return true;
        }
        if (SelectedTrigger != null)
        {
            x = 10000.0 + (SelectedTrigger.X * 0.25);
            y = 10000.0 - (SelectedTrigger.Y * 0.25);
            return true;
        }
        if (SelectedMesh != null)
        {
            var verts = SelectedMesh.Vertices2D;
            if (verts != null && verts.Count > 0)
            {
                x = verts.Average(p => p.X);
                y = verts.Average(p => p.Y);
                return true;
            }
        }
        if (SelectedPath != null)
        {
            if (SelectedPath.Nodes != null && SelectedPath.Nodes.Count > 0)
            {
                // Prefer a node that is actually inside the current viewport if any
                if (_scrollViewer != null)
                {
                    var left = _scrollViewer.Offset.X;
                    var top = _scrollViewer.Offset.Y;
                    var vpW = _scrollViewer.Viewport.Width / Math.Max(ZoomLevel, 0.0001);
                    var vpH = _scrollViewer.Viewport.Height / Math.Max(ZoomLevel, 0.0001);
                    var right = left + vpW;
                    var bottom = top + vpH;
                    var centerX = left + vpW / 2.0;
                    var centerY = top + vpH / 2.0;

                    var nodesWithCoords = SelectedPath.Nodes
                        .Select(n => new { n, cx = 10000.0 + (n.X * 0.25), cy = 10000.0 - (n.Y * 0.25) })
                        .ToList();

                    var visibleNodes = nodesWithCoords
                        .Where(p => p.cx >= left && p.cx <= right && p.cy >= top && p.cy <= bottom)
                        .OrderBy(p => (p.cx - centerX) * (p.cx - centerX) + (p.cy - centerY) * (p.cy - centerY))
                        .ToList();

                    if (visibleNodes.Count > 0)
                    {
                        x = visibleNodes[0].cx;
                        y = visibleNodes[0].cy;
                        return true;
                    }

                    // Otherwise, pick the node nearest to viewport center (even if off-screen) so the arrow points to it
                    var nearest = nodesWithCoords
                        .OrderBy(p => (p.cx - centerX) * (p.cx - centerX) + (p.cy - centerY) * (p.cy - centerY))
                        .First();
                    x = nearest.cx;
                    y = nearest.cy;
                    return true;
                }

                // Fallback: centroid of nodes
                var xs = SelectedPath.Nodes.Select(n => 10000.0 + (n.X * 0.25));
                var ys = SelectedPath.Nodes.Select(n => 10000.0 - (n.Y * 0.25));
                x = xs.Average();
                y = ys.Average();
                return true;
            }
        }

        return false;
    }

    private void PanUp()
    {
        if (_scrollViewer != null)
        {
            var newOffset = _scrollViewer.Offset + new Vector(0, -PanStep);
            _scrollViewer.Offset = newOffset;
        }
    }

    private void PanDown()
    {
        if (_scrollViewer != null)
        {
            var newOffset = _scrollViewer.Offset + new Vector(0, PanStep);
            _scrollViewer.Offset = newOffset;
        }
    }

    private void PanLeft()
    {
        if (_scrollViewer != null)
        {
            var newOffset = _scrollViewer.Offset + new Vector(-PanStep, 0);
            _scrollViewer.Offset = newOffset;
        }
    }

    private void PanRight()
    {
        if (_scrollViewer != null)
        {
            var newOffset = _scrollViewer.Offset + new Vector(PanStep, 0);
            _scrollViewer.Offset = newOffset;
        }
    }

    /// <summary>
    /// Exports the currently selected object as JSON to a file.
    /// </summary>
    private async void ExportSelectedAsJson()
    {
        object? selectedObject = GetSelectedObjectForExport();
        string? objectName = GetSelectedObjectName();

        if (selectedObject == null)
        {
            MessageService.Warn("No object selected to export.").Send();
            return;
        }

        try
        {
            var json = DeepJsonSerializer.SerializeDeep(selectedObject);

            // Open save dialog
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Object as JSON",
                Filters = new List<FileDialogFilter>
                {
                    new() { Name = "JSON Files", Extensions = { "json" } },
                    new() { Name = "All Files", Extensions = { "*" } }
                },
                InitialFileName = $"{objectName ?? "object"}.json"
            };

            var app = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;

            if (mainWindow != null)
            {
                var filePath = await saveDialog.ShowAsync(mainWindow);
                if (!string.IsNullOrEmpty(filePath))
                {
                    await System.IO.File.WriteAllTextAsync(filePath, json);
                    MessageService.Info($"Exported {objectName ?? "object"} to {filePath}").Send();
                }
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Failed to export object: {ex.Message}").Send();
        }
    }

    private object? GetSelectedObjectForExport()
    {
        // Check all possible selected objects with original data
        if (SelectedTrigger?.OriginalTrigger != null)
            return SelectedTrigger.OriginalTrigger;
        if (SelectedVolume?.OriginalVolume != null)
            return SelectedVolume.OriginalVolume;
        if (SelectedSpawnTemplate != null)
            return SelectedSpawnTemplate;
        if (SelectedTemplate != null)
            return SelectedTemplate;
        if (SelectedCoreObject != null)
            return SelectedCoreObject;

        // For visualization objects without original references, create a simple export object
        if (SelectedObject != null)
        {
            return new
            {
                SelectedObject.Name,
                SelectedObject.Type,
                SelectedObject.TemplateID,
                SelectedObject.X,
                SelectedObject.Y,
                SelectedObject.Z,
                SelectedObject.Scale,
                SelectedObject.Flags
            };
        }
        if (SelectedCollision != null)
        {
            return new
            {
                SelectedCollision.Name,
                SelectedCollision.GeometryType,
                SelectedCollision.X,
                SelectedCollision.Y,
                SelectedCollision.Z,
                SelectedCollision.Scale,
                SelectedCollision.Rotation,
                SelectedCollision.Material,
                SelectedCollision.CategoryFlags,
                SelectedCollision.CollisionFlags,
                SelectedCollision.GeometryParams
            };
        }
        if (SelectedPath != null)
        {
            return new
            {
                SelectedPath.Name,
                SelectedPath.PathId
            };
        }
        if (SelectedMesh != null)
        {
            return new
            {
                SelectedMesh.Name
            };
        }
        if (SelectedSpawn != null)
        {
            return new
            {
                SelectedSpawn.Name,
                SelectedSpawn.SpawnId,
                SelectedSpawn.TemplateId,
                SelectedSpawn.X,
                SelectedSpawn.Y,
                SelectedSpawn.Z,
                SelectedSpawn.Scale,
                SelectedSpawn.SpawnChance,
                SelectedSpawn.SpawnType,
                SelectedSpawn.PathId,
                SelectedSpawn.CreatureType,
                SelectedSpawn.PathIds
            };
        }

        return null;
    }

    private string? GetSelectedObjectName()
    {
        if (SelectedObject != null)
            return SelectedObject.Name;
        if (SelectedCoreObject != null)
            return SelectedCoreObject.ToString();
        if (SelectedCollision != null)
            return SelectedCollision.Name;
        if (SelectedPath != null)
            return SelectedPath.Name;
        if (SelectedMesh != null)
            return SelectedMesh.Name;
        if (SelectedVolume != null)
            return SelectedVolume.Name;
        if (SelectedTrigger != null)
            return SelectedTrigger.Name;
        if (SelectedSpawn != null)
            return SelectedSpawn.Name;
        if (SelectedTemplate != null)
            return SelectedTemplate.GetType().Name;

        return null;
    }

    /// <summary>
    /// Opens the editor to modify an existing teleport in the database.
    /// </summary>
    private async void EditTeleport(ResultWrapper wrapper)
    {
        if (wrapper?.OriginalResult is not ResTeleport resTeleport)
        {
            MessageService.Error("Can only edit ResTeleport results.").Send();
            return;
        }

        var zoneName = SelectedTrigger?.Zone ?? SelectedZone;
        var triggerName = wrapper.TriggerName ?? SelectedTrigger?.Name ?? "Unknown";

        if (string.IsNullOrEmpty(zoneName) || string.IsNullOrEmpty(triggerName))
        {
            MessageService.Error("Zone and trigger context required for database editing.").Send();
            return;
        }

        try
        {
            var zoneDataService = new ZoneDataService();
            var editor = new Controls.Results.DatabaseResTeleportEditor(resTeleport, zoneName, triggerName, zoneDataService, isNew: false);

            var app = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;

            if (mainWindow != null)
            {
                await editor.ShowDialog(mainWindow);
            }
            else
            {
                editor.Show();
            }

            if (editor.WasSaved)
            {
                MessageService.Info("Teleport data saved to database.").Send();
                wrapper.RefreshFromDatabase();
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Failed to edit teleport: {ex.Message}").Send();
        }
    }

    /// <summary>
    /// Opens the editor to create a new teleport in the database.
    /// </summary>
    private async void CreateTeleport(ResultWrapper wrapper)
    {
        if (wrapper?.OriginalResult is not ResTeleport resTeleport)
        {
            MessageService.Error("Can only create ResTeleport entries.").Send();
            return;
        }

        var zoneName = SelectedTrigger?.Zone ?? SelectedZone;
        var triggerName = wrapper.TriggerName ?? SelectedTrigger?.Name ?? "Unknown";

        if (string.IsNullOrEmpty(zoneName) || string.IsNullOrEmpty(triggerName))
        {
            MessageService.Error("Zone and trigger context required for database creation.").Send();
            return;
        }

        try
        {
            var zoneDataService = new ZoneDataService();
            var editor = new Controls.Results.DatabaseResTeleportEditor(resTeleport, zoneName, triggerName, zoneDataService, isNew: true);

            var app = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = app?.MainWindow;

            if (mainWindow != null)
            {
                await editor.ShowDialog(mainWindow);
            }
            else
            {
                editor.Show();
            }

            if (editor.WasSaved)
            {
                MessageService.Info("Teleport data created in database.").Send();
                wrapper.RefreshFromDatabase();
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Failed to create teleport: {ex.Message}").Send();
        }
    }

public void HandleMouseWheel(double delta, Avalonia.Point mousePosition)
    {
        var oldZoom = ZoomLevel;
        var zoomFactor = delta > 0 ? 1 + ZoomStep : 1 - ZoomStep;
        var newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, oldZoom * zoomFactor));
        
        if (Math.Abs(newZoom - oldZoom) > 0.001)
        {
            // Zoom towards mouse position
            var zoomRatio = newZoom / oldZoom;
            PanX = mousePosition.X - (mousePosition.X - PanX) * zoomRatio;
            PanY = mousePosition.Y - (mousePosition.Y - PanY) * zoomRatio;
            ZoomLevel = newZoom;
            UpdateViewportIndicator();
        }
    }

public void HandleMouseDrag(double deltaX, double deltaY)
    {
        if (_scrollViewer != null)
        {
            var newOffset = _scrollViewer.Offset + new Vector(-deltaX, -deltaY);
            _scrollViewer.Offset = newOffset;
            UpdateViewportIndicator();
        }
    }

public void UpdateCanvasTransform()
    {
        if (_zoneObjectCanvas?.Parent is Canvas mainCanvas)
        {
            // Apply zoom to the main canvas
            var scaleTransform = new ScaleTransform(ZoomLevel, ZoomLevel);
            mainCanvas.RenderTransform = scaleTransform;
        }
        
        // Keep overlays in sync
        UpdateViewportIndicator();
        
        // Update zoom display
        this.RaisePropertyChanged(nameof(ZoomDisplayText));
    }

    private void CreateVisualObjects()
    {
        if (_zoneObjectCanvas == null)
        {
            Console.WriteLine("Canvas not set, cannot create visual objects");
            return;
        }
        
        Console.WriteLine($"Creating visual objects on canvas for {ZoneVisualizationObjects.Count} objects");
        
        // Clear existing objects
        _zoneObjectCanvas.Children.Clear();
        
        foreach (var obj in ZoneVisualizationObjects)
        {
            try
            {
                // Convert coordinates
                var canvasX = 10000.0 + (obj.X * 0.25);
                var canvasY = 10000.0 - (obj.Y * 0.25);
                
                // Create visual element
                var border = new Border
                {
                    Background = GetObjectColorBrush(obj.Type),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(3),
                    Width = 16,
                    Height = 16
                };
                
                var textBlock = new TextBlock
                {
                    Text = GetObjectIcon(obj.Type),
                    FontSize = 10,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                
                border.Child = textBlock;
                
                // Position on canvas
                Canvas.SetLeft(border, canvasX);
                Canvas.SetTop(border, canvasY);
                
                // Add tooltip
                ToolTip.SetTip(border, $"{obj.Name} ({obj.Type}) at ({obj.X:F1}, {obj.Y:F1})");
                
                // Tag for identification
                border.Tag = obj;
                
                // Add click handler for selection
                border.PointerPressed += (sender, e) => {
                    if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
                    {
                        // Select this object in the hierarchy and properties
                        SelectedObject = obj;
                        e.Handled = true;
                    }
                };
                
                // Set cursor to indicate clickable
                border.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
                
                // Set initial visibility
                border.IsVisible = ShowZoneObjects;
                
                // Add to canvas
                _zoneObjectCanvas.Children.Add(border);
                
                // Debug for first few objects
                if (_zoneObjectCanvas.Children.Count <= 5)
                {
                    Console.WriteLine($"Added object '{obj.Name}' at canvas position ({canvasX:F1}, {canvasY:F1})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating visual for object '{obj.Name}': {ex.Message}");
            }
        }
        
        Console.WriteLine($"Created {_zoneObjectCanvas.Children.Count} visual objects on canvas");
        
        // Update visual selection highlights
        UpdateVisualSelection();
        
        // Create collision visuals if available and enabled
        CreateCollisionVisuals();
    }

    private bool ShouldShowCollision(CollisionVisualizationObject collision)
    {
        if (!ShowCollisions)
            return false;

        // Check shape-based filters
        var geometryType = collision.GeometryType;
        
        return geometryType switch
        {
            "Box" => ShowBoxCollisions,
            "Sphere" => ShowSphereCollisions,
            "Cylinder" => ShowCylinderCollisions,
            "Tube" => ShowTubeCollisions,
            "Plane" => ShowPlaneCollisions,
            "Mesh" => ShowMeshCollisions,
            "Ray" => ShowRayCollisions,
            _ => true // Show unknown geometry types by default
        };
    }

    private void UpdateVisibility()
    {
        if (_zoneObjectCanvas == null)
            return;
            
        // Update zone object visibility
        var zoneObjectVisuals = _zoneObjectCanvas.Children
            .OfType<Control>()
            .Where(c => c.Tag is ZoneVisualizationObject)
            .ToList();
            
        foreach (var visual in zoneObjectVisuals)
        {
            visual.IsVisible = ShowZoneObjects;
        }
        
        // Update collision object visibility
        var collisionVisuals = _zoneObjectCanvas.Children
            .OfType<Control>()
            .Where(c => c.Tag is CollisionVisualizationObject)
            .ToList();
            
        foreach (var visual in collisionVisuals)
        {
            if (visual.Tag is CollisionVisualizationObject collision)
            {
                visual.IsVisible = ShouldShowCollision(collision);
            }
        }
        
        // Update NIF geometry visibility
        var nifGeometryVisuals = _zoneObjectCanvas.Children
            .OfType<Control>()
            .Where(c => c.Tag is NifMeshVisualizationObject)
            .ToList();
            
        foreach (var visual in nifGeometryVisuals)
        {
            visual.IsVisible = ShowNifGeometry;
        }
        
        // Update spawn visibility
        var spawnVisuals = _zoneObjectCanvas.Children
            .OfType<Control>()
            .Where(c => c.Tag is SpawnVisualizationObject)
            .ToList();
            
        foreach (var visual in spawnVisuals)
        {
            visual.IsVisible = ShowSpawns;
        }
        
        // Update path visibility (path lines and arrows)
        var pathLineVisuals = _zoneObjectCanvas.Children
            .OfType<Control>()
            .Where(c => c.Tag is PathLineTag)
            .ToList();
            
        foreach (var visual in pathLineVisuals)
        {
            visual.IsVisible = ShowPaths;
        }
        
        // Update node visibility
        var nodeVisuals = _zoneObjectCanvas.Children
            .OfType<Control>()
            .Where(c => c.Tag is NodeVisualizationObject)
            .ToList();
            
        foreach (var visual in nodeVisuals)
        {
            visual.IsVisible = ShowNodes;
        }
        
        // Update volume visibility
        var volumeVisuals = _zoneObjectCanvas.Children
            .OfType<Control>()
            .Where(c => c.Tag is VolumeVisualizationObject)
            .ToList();
            
        foreach (var visual in volumeVisuals)
        {
            visual.IsVisible = ShowVolumes;
        }
        
        // Notify property changes for count updates
        this.RaisePropertyChanged(nameof(VisibleCollisionCount));
    }

    private void CreateCollisionVisuals()
    {
        if (_zoneObjectCanvas == null)
        {
            Console.WriteLine("Canvas not set, cannot create collision visuals");
            return;
        }
        
        foreach (var collision in CollisionVisualizationObjects)
        {
            try
            {
                var shouldShow = ShouldShowCollision(collision);
                var visual = CreateCollisionShapeVisual(collision);
                if (visual != null)
                {
                    // Set initial visibility based on filters
                    visual.IsVisible = shouldShow;
                    _zoneObjectCanvas.Children.Add(visual);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating collision visual for '{collision.Name}': {ex.Message}");
            }
        }
        
    }

    public void RebuildSceneVisuals()
    {
        if (_zoneObjectCanvas == null)
            return;
        try
        {
            _zoneObjectCanvas.Children.Clear();
            CreateVisualObjects();
            CreatePathVisuals();
            CreateVolumeVisuals();
            CreateNifGeometryVisuals();
            UpdateVisibility();
            UpdateVisualSelection();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Failed to rebuild scene visuals: {ex.Message}");
        }
    }

    private void CreateNifGeometryVisuals()
    {
        if (_zoneObjectCanvas == null)
        {
            Console.WriteLine("Canvas not set, cannot create NIF geometry visuals");
            return;
        }
        
        Console.WriteLine($"Creating NIF geometry visuals for {NifMeshVisualizationObjects.Count} mesh objects");
        
        foreach (var mesh in NifMeshVisualizationObjects)
        {
            try
            {
                var visual = CreateNifMeshVisual(mesh);
                if (visual != null)
                {
                    // Set initial visibility based on ShowNifGeometry setting
                    visual.IsVisible = ShowNifGeometry;
                    _zoneObjectCanvas.Children.Add(visual);
                    Console.WriteLine($"Added NIF mesh visual for '{mesh.Name}' to canvas (visible: {visual.IsVisible})");
                }
                else
                {
                    Console.WriteLine($"Failed to create visual for NIF mesh '{mesh.Name}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating NIF mesh visual for '{mesh.Name}': {ex.Message}");
            }
        }
        
        Console.WriteLine($"Created NIF geometry visuals on canvas");
    }
    
    private void CreatePathVisuals()
    {
        if (_zoneObjectCanvas == null)
        {
            Console.WriteLine("ERROR: Canvas not set, cannot create path visuals");
            return;
        }
        
        Console.WriteLine("=== CREATING ROBUST PATH VISUALIZATIONS ===");
        Console.WriteLine($"Data counts: {SpawnVisualizationObjects.Count} spawns, {PathVisualizationObjects.Count} paths, {NodeVisualizationObjects.Count} nodes");
        Console.WriteLine($"Visibility flags: Spawns={ShowSpawns}, Paths={ShowPaths}, Nodes={ShowNodes}");
        
        int visualsCreated = 0;
        int visualsSkipped = 0;
        
        // STEP 1: Create individual node visuals (as circles)
        if (ShowNodes && NodeVisualizationObjects.Count > 0)
        {
            Console.WriteLine($"STEP 1: Creating {NodeVisualizationObjects.Count} node visuals...");
            
            foreach (var node in NodeVisualizationObjects)
            {
                try
                {
                    var nodeVisual = CreateEnhancedNodeVisual(node);
                    if (nodeVisual != null)
                    {
                        _zoneObjectCanvas.Children.Add(nodeVisual);
                        visualsCreated++;
                    }
                    else
                    {
                        visualsSkipped++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR creating node visual for '{node.Name}': {ex.Message}");
                    visualsSkipped++;
                }
            }
            Console.WriteLine($"STEP 1 COMPLETE: Created {visualsCreated} node visuals, skipped {visualsSkipped}");
        }
        else
        {
            Console.WriteLine($"STEP 1 SKIPPED: ShowNodes={ShowNodes}, NodeCount={NodeVisualizationObjects.Count}");
        }
        
        // STEP 2: Create path line visuals (connecting nodes with arrows)
        if (ShowPaths && PathVisualizationObjects.Count > 0)
        {
            Console.WriteLine($"STEP 2: Creating path line visuals for {PathVisualizationObjects.Count} paths...");
            
            int pathsWithVisuals = 0;
            int totalLineSegments = 0;
            
            foreach (var path in PathVisualizationObjects)
            {
                try
                {
                    if (path.Nodes != null && path.Nodes.Count >= 2)
                    {
                        var pathVisuals = CreateEnhancedPathLineVisuals(path);
                        foreach (var visual in pathVisuals)
                        {
                            _zoneObjectCanvas.Children.Add(visual);
                            totalLineSegments++;
                        }
                        pathsWithVisuals++;
                        
                        if (pathsWithVisuals <= 3) // Log first few for debugging
                        {
                            Console.WriteLine($"  Path '{path.Name}': {path.Nodes.Count} nodes, {pathVisuals.Count} line segments");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  Skipping path '{path.Name}': {path.Nodes?.Count ?? 0} nodes (need at least 2)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR creating path visuals for '{path.Name}': {ex.Message}");
                }
            }
            Console.WriteLine($"STEP 2 COMPLETE: Created visuals for {pathsWithVisuals} paths with {totalLineSegments} line segments");
        }
        else
        {
            Console.WriteLine($"STEP 2 SKIPPED: ShowPaths={ShowPaths}, PathCount={PathVisualizationObjects.Count}");
        }
        
        // STEP 3: Create spawn creature visuals (as larger colored shapes)
        if (ShowSpawns && SpawnVisualizationObjects.Count > 0)
        {
            Console.WriteLine($"STEP 3: Creating {SpawnVisualizationObjects.Count} spawn visuals...");
            
            int spawnsCreated = 0;
            int spawnsWithPaths = 0;
            
            foreach (var spawn in SpawnVisualizationObjects)
            {
                try
                {
                    var spawnVisual = CreateEnhancedSpawnVisual(spawn);
                    if (spawnVisual != null)
                    {
                        _zoneObjectCanvas.Children.Add(spawnVisual);
                        spawnsCreated++;
                        
                        if (spawn.PathId != 0)
                        {
                            spawnsWithPaths++;
                        }
                        
                        if (spawnsCreated <= 5) // Log first few for debugging
                        {
                            Console.WriteLine($"  Spawn '{spawn.Name}': Pos=({spawn.X:F1}, {spawn.Y:F1}), PathID={spawn.PathId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR creating spawn visual for '{spawn.Name}': {ex.Message}");
                }
            }
            Console.WriteLine($"STEP 3 COMPLETE: Created {spawnsCreated} spawn visuals, {spawnsWithPaths} have path associations");
        }
        else
        {
            Console.WriteLine($"STEP 3 SKIPPED: ShowSpawns={ShowSpawns}, SpawnCount={SpawnVisualizationObjects.Count}");
        }
        
        Console.WriteLine("=== VISUALIZATION SUMMARY ===");
        Console.WriteLine($"Total canvas children: {_zoneObjectCanvas.Children.Count}");
        Console.WriteLine($"Canvas size: {_zoneObjectCanvas.Width} x {_zoneObjectCanvas.Height}");
        
        // Additional debugging for empty visualizations
        if (_zoneObjectCanvas.Children.Count == 0)
        {
            Console.WriteLine("WARNING: No visual elements were added to canvas!");
            Console.WriteLine("This could be due to:");
            Console.WriteLine("  - All visibility flags are false");
            Console.WriteLine("  - All data collections are empty");
            Console.WriteLine("  - Coordinate transformation issues");
            Console.WriteLine("  - Canvas positioning problems");
        }
    }

    // Enhanced visual creation methods for robust 2D path visualization
    private Control? CreateEnhancedNodeVisual(NodeVisualizationObject node)
    {
        // Convert 3D world coordinates to 2D canvas coordinates
        var canvasX = 10000.0 + (node.X * 0.25);
        var canvasY = 10000.0 - (node.Y * 0.25); // Flip Y axis for proper display
        
        // Create node visual as a colored circle with better styling
        var nodeColor = node.PathId != 0 ? Avalonia.Media.Color.FromRgb(0, 255, 0) : Avalonia.Media.Color.FromRgb(128, 128, 128); // Green if part of path, gray if orphaned
        var nodeSize = node.PathId != 0 ? 8.0 : 6.0; // Larger if part of path
        
        var nodeVisual = new Ellipse
        {
            Width = nodeSize,
            Height = nodeSize,
            Fill = new SolidColorBrush(nodeColor),
            Stroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 255)), // White border
            StrokeThickness = 1,
            IsHitTestVisible = true,
            Tag = node // Set tag for filtering
        };
        
        // Add tooltip with node information
        ToolTip.SetTip(nodeVisual, $"Node {node.NodeId}\nPath: {node.PathId}\nPosition: ({node.X:F1}, {node.Y:F1}, {node.Z:F1})\nIndex: {node.NodeIndex}");
        
        // Add click handler to select the path this node belongs to
        nodeVisual.PointerPressed += (sender, e) =>
        {
            if (e.GetCurrentPoint((Control)sender).Properties.IsLeftButtonPressed)
            {
                // Find the path this node belongs to
                var pathObj = PathVisualizationObjects.FirstOrDefault(p => p.PathId == node.PathId);
                if (pathObj != null)
                {
                    SelectPath(pathObj);
                    Console.WriteLine($"Selected path {pathObj.Name} via node {node.NodeId}");
                }
                e.Handled = true;
            }
        };
        
        // Position on canvas (center the circle)
        Canvas.SetLeft(nodeVisual, canvasX - nodeSize / 2);
        Canvas.SetTop(nodeVisual, canvasY - nodeSize / 2);
        // Canvas.SetZIndex(nodeVisual, 3); // ZIndex not available in this context
        
        return nodeVisual;
    }
    
    private Control? CreateEnhancedSpawnVisual(SpawnVisualizationObject spawn)
    {
        // Convert 3D world coordinates to 2D canvas coordinates
        var canvasX = 10000.0 + (spawn.X * 0.25);
        var canvasY = 10000.0 - (spawn.Y * 0.25); // Flip Y axis for proper display
        
        // Create spawn visual as a larger, more prominent shape
        var spawnColor = spawn.PathId != 0 ? Avalonia.Media.Color.FromRgb(255, 140, 0) : Avalonia.Media.Color.FromRgb(255, 0, 0); // Orange if has path, red if static
        var spawnSize = 14.0;
        
        // Use a diamond shape for spawns to distinguish from circular nodes
        var spawnVisual = new Border
        {
            Width = spawnSize,
            Height = spawnSize,
            Background = new SolidColorBrush(spawnColor),
            BorderBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(2),
            IsHitTestVisible = true,
            Tag = spawn, // Set tag for filtering
            Child = new TextBlock
            {
                Text = "C", // C for Creature
                Foreground = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 255)),
                FontSize = 8,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            }
        };
        
        // Add comprehensive tooltip
        ToolTip.SetTip(spawnVisual, $"Spawn: {spawn.Name}\nID: {spawn.SpawnId}\nPath: {spawn.PathId}\nType: {spawn.CreatureType}\nPosition: ({spawn.X:F1}, {spawn.Y:F1}, {spawn.Z:F1})");
        
        // Add click handler to select this spawn
        spawnVisual.PointerPressed += (sender, e) =>
        {
            if (e.GetCurrentPoint((Control)sender).Properties.IsLeftButtonPressed)
            {
                SelectSpawn(spawn);
                Console.WriteLine($"Selected spawn {spawn.Name}");
                e.Handled = true;
            }
        };
        
        // Position on canvas (center the shape)
        Canvas.SetLeft(spawnVisual, canvasX - spawnSize / 2);
        Canvas.SetTop(spawnVisual, canvasY - spawnSize / 2);
        // Canvas.SetZIndex(spawnVisual, 5); // ZIndex not available in this context
        
        return spawnVisual;
    }
    
    private List<Control> CreateEnhancedPathLineVisuals(PathVisualizationObject path)
    {
        var visuals = new List<Control>();
        
        if (path.Nodes == null || path.Nodes.Count < 2) return visuals;
        
        // Uniform color for all paths
        var pathColor = PathBaseColor;
        
        // Create line segments connecting consecutive nodes
        for (int i = 0; i < path.Nodes.Count - 1; i++)
        {
            var fromNode = path.Nodes[i];
            var toNode = path.Nodes[i + 1];
            
            // Convert coordinates
            var fromX = 10000.0 + (fromNode.X * 0.25);
            var fromY = 10000.0 - (fromNode.Y * 0.25);
            var toX = 10000.0 + (toNode.X * 0.25);
            var toY = 10000.0 - (toNode.Y * 0.25);
            
            // Create main path line
            var pathLine = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(fromX, fromY),
                EndPoint = new Avalonia.Point(toX, toY),
                Stroke = PathBaseBrush,
                StrokeThickness = 2,
                IsHitTestVisible = true,
                Tag = new PathLineTag { Path = path, IsArrow = false } // Custom tag for path components
            };
            
            // Add tooltip
            ToolTip.SetTip(pathLine, $"Path: {path.Name}\nSegment {i + 1}/{path.Nodes.Count - 1}\nFrom Node {fromNode.NodeId} to Node {toNode.NodeId}");
            
            // Add click handler to select this path
            pathLine.PointerPressed += (sender, e) =>
            {
                if (e.GetCurrentPoint((Control)sender).Properties.IsLeftButtonPressed)
                {
                    SelectPath(path);
                    Console.WriteLine($"Selected path {path.Name} via path line");
                    e.Handled = true;
                }
            };
            
            // Canvas.SetZIndex(pathLine, 2); // ZIndex not available in this context
            visuals.Add(pathLine);
            
            // Create directional arrow at the end of each segment
            var arrowVisual = CreateArrowHead(fromX, fromY, toX, toY, PathBaseColor);
            if (arrowVisual != null)
            {
                // Set the correct path reference for the arrow
                arrowVisual.Tag = new PathLineTag { Path = path, IsArrow = true };
                
                // Add click handler to select this path
                arrowVisual.PointerPressed += (sender, e) =>
                {
                    if (e.GetCurrentPoint((Control)sender).Properties.IsLeftButtonPressed)
                    {
                        SelectPath(path);
                        Console.WriteLine($"Selected path {path.Name} via arrow");
                        e.Handled = true;
                    }
                };
                
                // Canvas.SetZIndex(arrowVisual, 2); // ZIndex not available in this context
                visuals.Add(arrowVisual);
            }
        }
        
        return visuals;
    }
    
    private Control? CreateArrowHead(double fromX, double fromY, double toX, double toY, Avalonia.Media.Color color)
    {
        // Calculate arrow direction
        var dx = toX - fromX;
        var dy = toY - fromY;
        var length = Math.Sqrt(dx * dx + dy * dy);
        
        if (length < 0.1) return null; // Too short to create meaningful arrow
        
        // Normalize direction
        dx /= length;
        dy /= length;
        
        // Arrow parameters
        var arrowLength = 8.0;
        var arrowAngle = Math.PI / 6; // 30 degrees
        
        // Calculate arrow head points
        var arrowX = toX - dx * arrowLength;
        var arrowY = toY - dy * arrowLength;
        
        var perpX = -dy; // Perpendicular vector
        var perpY = dx;
        
        var arrowLeft = new Avalonia.Point(
            arrowX + perpX * arrowLength * Math.Sin(arrowAngle),
            arrowY + perpY * arrowLength * Math.Sin(arrowAngle)
        );
        
        var arrowRight = new Avalonia.Point(
            arrowX - perpX * arrowLength * Math.Sin(arrowAngle),
            arrowY - perpY * arrowLength * Math.Sin(arrowAngle)
        );
        
        // Create arrow head as a polygon
        var arrow = new Polygon
        {
            Points = new List<Avalonia.Point> { new Avalonia.Point(toX, toY), arrowLeft, arrowRight },
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1,
            Tag = new PathLineTag { Path = null, IsArrow = true } // Will be set by caller
        };
        
        return arrow;
    }
    
    // Helper methods for path details
    private string GetPathSpawnCount(PathVisualizationObject path)
    {
        var spawnsOnPath = GetSpawnsForPath(path);
        var count = spawnsOnPath.Count;
        return count > 0 ? $"Spawns: {count}" : "No spawns";
    }
    
    private List<SpawnVisualizationObject> GetSpawnsForPath(PathVisualizationObject path)
    {
        return SpawnVisualizationObjects.Where(spawn => spawn.PathId == path.PathId).ToList();
    }
    
    private string GetPathDetailedDescription(PathVisualizationObject path)
    {
        var nodes = path.Nodes;
        var spawns = GetSpawnsForPath(path);
        var description = new List<string>();
        
        description.Add($"Path '{path.Name}' (ID: {path.PathId})");
        description.Add($"Type: {path.PathType}");
        
        if (nodes.Count > 0)
        {
            description.Add($"Route: {nodes.Count} waypoints");
            var totalDistance = CalculatePathDistance(nodes);
            description.Add($"Total distance: {totalDistance:F1} units");
            
            // Show first and last nodes
            if (nodes.Count >= 2)
            {
                var firstNode = nodes.First();
                var lastNode = nodes.Last();
                description.Add($"Start: ({firstNode.X:F1}, {firstNode.Y:F1}, {firstNode.Z:F1})");
                description.Add($"End: ({lastNode.X:F1}, {lastNode.Y:F1}, {lastNode.Z:F1})");
            }
        }
        else
        {
            description.Add("No waypoints defined");
        }
        
        if (spawns.Count > 0)
        {
            description.Add($"Used by {spawns.Count} creature(s):");
            foreach (var spawn in spawns.Take(5)) // Show first 5 spawns
            {
                description.Add($"  • {spawn.Name} ({spawn.CreatureType})");
            }
            if (spawns.Count > 5)
            {
                description.Add($"  ... and {spawns.Count - 5} more");
            }
        }
        else
        {
            description.Add("Not used by any creatures");
        }
        
        return string.Join("\n", description);
    }
    
    private float CalculatePathDistance(List<NodeVisualizationObject> nodes)
    {
        if (nodes.Count < 2) return 0;
        
        float totalDistance = 0;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            var from = nodes[i];
            var to = nodes[i + 1];
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var dz = to.Z - from.Z;
            totalDistance += (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        return totalDistance;
    }
    
    private void CreateVolumeVisuals()
    {
        if (_zoneObjectCanvas == null)
        {
            Console.WriteLine("Canvas not set, cannot create volume visuals");
            return;
        }
        
        Console.WriteLine($"Creating volume visuals for {VolumeVisualizationObjects.Count} volumes");
        
        foreach (var volume in VolumeVisualizationObjects)
        {
            try
            {
                var visual = CreateVolumeVisual(volume);
                if (visual != null)
                {
                    visual.IsVisible = ShowVolumes;
                    _zoneObjectCanvas.Children.Add(visual);
                    Console.WriteLine($"Added volume visual for '{volume.Name}' to canvas (visible: {visual.IsVisible})");
                }
                else
                {
                    Console.WriteLine($"Failed to create visual for volume '{volume.Name}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating volume visual for '{volume.Name}': {ex.Message}");
            }
        }
        
        Console.WriteLine($"Created volume visuals on canvas");
    }
    
    private Control? CreateSpawnVisual(SpawnVisualizationObject spawn)
    {
        // Convert coordinates (same as zone objects)
        var canvasX = 10000.0 + (spawn.X * 0.25);
        var canvasY = 10000.0 - (spawn.Y * 0.25);
        
        Console.WriteLine($"Creating spawn visual for '{spawn.Name}' at game coords ({spawn.X:F1}, {spawn.Y:F1}, {spawn.Z:F1}) -> canvas coords ({canvasX:F1}, {canvasY:F1})");
        
        // Use a distinctive color for spawns - bright orange
        var spawnBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 140, 0)); // Orange
        
        var spawn_visual = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = spawnBrush,
            Stroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 255)), // White outline
            StrokeThickness = 2
        };
        
        // Position on canvas
        Canvas.SetLeft(spawn_visual, canvasX - 6); // Center the circle
        Canvas.SetTop(spawn_visual, canvasY - 6);
        
        // Add tooltip
        ToolTip.SetTip(spawn_visual, $"{spawn.Name} ({spawn.SpawnType})\nID: {spawn.SpawnId}\nTemplate: {spawn.TemplateId}\nAt: ({spawn.X:F1}, {spawn.Y:F1}, {spawn.Z:F1})\n{spawn.PathInfo}");
        
        // Tag for identification
        spawn_visual.Tag = spawn;
        
        return spawn_visual;
    }
    
    private Control? CreateNodeVisual(NodeVisualizationObject node)
    {
        // Convert coordinates (same as zone objects)
        var canvasX = 10000.0 + (node.X * 0.25);
        var canvasY = 10000.0 - (node.Y * 0.25);
        
        // Use a distinctive color for nodes - bright green
        var nodeBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(0, 255, 0)); // Bright green
        
        var node_visual = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = nodeBrush,
            Stroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 255)), // White outline
            StrokeThickness = 1
        };
        
        // Position on canvas
        Canvas.SetLeft(node_visual, canvasX - 3); // Center the circle
        Canvas.SetTop(node_visual, canvasY - 3);
        
        // Add tooltip
        ToolTip.SetTip(node_visual, $"{node.DisplayText}\nID: {node.NodeId}\nPath: {node.PathId}\nAt: ({node.X:F1}, {node.Y:F1}, {node.Z:F1})");
        
        // Tag for identification
        node_visual.Tag = node;
        
        return node_visual;
    }
    
    private List<Control> CreatePathLineVisuals(PathVisualizationObject path)
    {
        var visuals = new List<Control>();
        
        if (path.Nodes.Count < 2) return visuals;
        
        // Create lines connecting consecutive nodes
        for (int i = 0; i < path.Nodes.Count - 1; i++)
        {
            var fromNode = path.Nodes[i];
            var toNode = path.Nodes[i + 1];
            
            // Convert coordinates
            var fromX = 10000.0 + (fromNode.X * 0.25);
            var fromY = 10000.0 - (fromNode.Y * 0.25);
            var toX = 10000.0 + (toNode.X * 0.25);
            var toY = 10000.0 - (toNode.Y * 0.25);
            
            // Create line
            var line = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(fromX, fromY),
                EndPoint = new Avalonia.Point(toX, toY),
                Stroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 0)), // Yellow path lines
                StrokeThickness = 2,
                Opacity = 0.8
            };
            
            // Add tooltip
            ToolTip.SetTip(line, $"{path.Name}\nSegment {i + 1} of {path.Nodes.Count - 1}\nFrom: {fromNode.DisplayText}\nTo: {toNode.DisplayText}");
            
            // Tag for identification
            line.Tag = path;
            
            visuals.Add(line);
        }
        
        return visuals;
    }

    private Control? CreateNifMeshVisual(NifMeshVisualizationObject mesh)
    {
        if (mesh.Vertices2D.Count == 0)
            return null;

        try
        {
            // For large meshes, use simplified representation
            var vertices = mesh.Vertices2D.Count > 100 
                ? NifGeometryProcessor.SimplifyMesh(mesh.Vertices2D, 100)
                : mesh.Vertices2D;

            // If we still have too many vertices, create a bounding box representation
            if (vertices.Count > 50)
            {
                return CreateNifBoundingBoxVisual(mesh);
            }

            // Create a polygon for the mesh
            var polygon = new Polygon
            {
                Points = vertices,
                Fill = new SolidColorBrush(Avalonia.Media.Color.FromArgb(100, 255, 255, 0)), // Semi-transparent yellow
                Stroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 0)), // Yellow outline
                StrokeThickness = 1,
                Opacity = 0.7
            };

            // Add tooltip
            ToolTip.SetTip(polygon, $"NIF Mesh: {mesh.Name}\nVertices: {mesh.VertexCount}\nTriangles: {mesh.TriangleCount}");
            
            // Tag for identification
            polygon.Tag = mesh;

            return polygon;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating polygon for mesh '{mesh.Name}': {ex.Message}");
            return CreateNifBoundingBoxVisual(mesh);
        }
    }
    
    /// <summary>
    /// Finds triggers that match the given event name in their activate or deactivate events
    /// </summary>
    public List<TriggerVisualizationObject> FindTriggersForEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName) || TriggerVisualizationObjects == null)
            return new List<TriggerVisualizationObject>();
            
        return TriggerVisualizationObjects
            .Where(trigger => 
                trigger.ActivateEvents.Contains(eventName) || 
                trigger.DeactivateEvents.Contains(eventName) ||
                trigger.FireEvents.Contains(eventName))
            .ToList();
    }
    
    /// <summary>
    /// Finds volumes that match the given event name in their enter or exit events
    /// </summary>
    public List<VolumeVisualizationObject> FindVolumesForEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName) || VolumeVisualizationObjects == null)
            return new List<VolumeVisualizationObject>();
            
        return VolumeVisualizationObjects
            .Where(volume => 
                volume.EnterEvents.Contains(eventName) || 
                volume.ExitEvents.Contains(eventName))
            .ToList();
    }
    
    /// <summary>
    /// Command to select a trigger from a volume's related triggers
    /// </summary>
    public ReactiveCommand<TriggerVisualizationObject, Unit> SelectRelatedTriggerCommand { get; }
    
    /// <summary>
    /// Command to select a volume from a trigger's related volumes
    /// </summary>
    public ReactiveCommand<VolumeVisualizationObject, Unit> SelectRelatedVolumeCommand { get; }
    
    private void SelectRelatedTrigger(TriggerVisualizationObject trigger)
    {
        if (trigger == null) return;
        
        // Clear other selections
        SelectedObject = null;
        SelectedCollision = null;
        SelectedPath = null;
        SelectedMesh = null;
        SelectedVolume = null;
        
        // Select the trigger
        SelectedTrigger = trigger;
        
        Debug.WriteLine($"Selected related trigger: {trigger.Name}");
    }
    
    private void SelectRelatedVolume(VolumeVisualizationObject volume)
    {
        if (volume == null) return;
        
        // Clear other selections
        SelectedObject = null;
        SelectedCollision = null;
        SelectedPath = null;
        SelectedMesh = null;
        SelectedTrigger = null;
        
        // Select the volume
        SelectedVolume = volume;
        
        Debug.WriteLine($"Selected related volume: {volume.Name}");
    }

    private Control CreateNifBoundingBoxVisual(NifMeshVisualizationObject mesh)
    {
        // Create a simple rectangle representing the mesh bounds
        if (mesh.BoundingBox.Count >= 4)
        {
            var minX = mesh.BoundingBox.Min(p => p.X);
            var maxX = mesh.BoundingBox.Max(p => p.X);
            var minY = mesh.BoundingBox.Min(p => p.Y);
            var maxY = mesh.BoundingBox.Max(p => p.Y);

            var rectangle = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = Math.Max(2, maxX - minX),
                Height = Math.Max(2, maxY - minY),
                Fill = new SolidColorBrush(Avalonia.Media.Color.FromArgb(80, 255, 255, 0)), // Semi-transparent yellow
                Stroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 0)), // Yellow outline
                StrokeThickness = 1,
                Opacity = 0.6
            };

            // Position the rectangle
            Canvas.SetLeft(rectangle, minX);
            Canvas.SetTop(rectangle, minY);

            // Add tooltip
            ToolTip.SetTip(rectangle, $"NIF Mesh (Simplified): {mesh.Name}\nVertices: {mesh.VertexCount}\nTriangles: {mesh.TriangleCount}");
            
            // Tag for identification
            rectangle.Tag = mesh;

            return rectangle;
        }

        // Fallback: create a small marker
        var marker = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 0)),
            Stroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 200, 0)),
            StrokeThickness = 1
        };

        // Center at canvas origin
        Canvas.SetLeft(marker, CanvasCenterX - 4);
        Canvas.SetTop(marker, CanvasCenterY - 4);

        // Add tooltip
        ToolTip.SetTip(marker, $"NIF Mesh (Marker): {mesh.Name}\nVertices: {mesh.VertexCount}");
        
        // Tag for identification
        marker.Tag = mesh;

        return marker;
    }
    
    private Control? CreateVolumeVisual(VolumeVisualizationObject volume)
    {
        // Convert coordinates (same as zone objects)
        var canvasX = 10000.0 + (volume.X * 0.25);
        var canvasY = 10000.0 - (volume.Y * 0.25);
        
        // Use red color as requested
        var volumeBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 0, 0)); // Red
        var volumeStroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(200, 0, 0)); // Darker red for stroke
        
        Control? visual = null;
        
        // Create shape based on primitive type
        var primitiveType = volume.PrimitiveType?.ToLowerInvariant() ?? "unknown";
        switch (primitiveType)
        {
            case "box":
            case "cube":
                visual = CreateVolumeBoxVisual(volume);
                break;
            case "sphere":
            case "ball":
                visual = CreateVolumeSphereVisual(volume);
                break;
            case "cylinder":
                visual = CreateVolumeCylinderVisual(volume);
                break;
            default:
                // Fallback to box for unknown types
                visual = CreateVolumeBoxVisual(volume);
                break;
        }
        
        if (visual != null)
        {
            // Position on canvas
            Canvas.SetLeft(visual, canvasX);
            Canvas.SetTop(visual, canvasY);
            
            // Add tooltip
            ToolTip.SetTip(visual, $"{volume.Name} ({volume.PrimitiveType})\n{volume.DimensionsText}\nAt: ({volume.X:F1}, {volume.Y:F1}, {volume.Z:F1})\n{volume.EventsText}");
            
            // Tag for identification and selection
            visual.Tag = volume;
            
            // Add click handler for selection
            visual.PointerPressed += (sender, e) => {
                if (e.GetCurrentPoint(visual).Properties.IsLeftButtonPressed)
                {
                    SelectedVolume = volume;
                    e.Handled = true;
                }
            };
            
            // Set cursor to indicate clickable
            visual.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        }
        
        return visual;
    }
    
    private Control CreateVolumeBoxVisual(VolumeVisualizationObject volume)
    {
        var width = Math.Max(10, volume.Width * 0.25);
        var height = Math.Max(10, volume.Length * 0.25);
        
        // Create a canvas to hold the shape and diagonal lines
        var canvas = new Canvas
        {
            Width = width,
            Height = height
        };
        
        // Create the rectangle outline
        var rectangle = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Fill = Brushes.Transparent, // No solid fill
            Stroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 0, 0)),
            StrokeThickness = 2
        };
        canvas.Children.Add(rectangle);
        
        // Add diagonal lines for shading
        AddDiagonalLines(canvas, width, height);
        
        return canvas;
    }
    
    private Control CreateVolumeSphereVisual(VolumeVisualizationObject volume)
    {
        var diameter = Math.Max(10, volume.Radius * 2 * 0.25);
        
        // Create a canvas to hold the shape and diagonal lines
        var canvas = new Canvas
        {
            Width = diameter,
            Height = diameter
        };
        
        // Create the ellipse outline
        var ellipse = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = Brushes.Transparent, // No solid fill
            Stroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 0, 0)),
            StrokeThickness = 2
        };
        canvas.Children.Add(ellipse);
        
        // Add diagonal lines for shading (clipped to circle)
        AddDiagonalLinesClipped(canvas, diameter, diameter, true);
        
        return canvas;
    }
    
    private Control CreateVolumeCylinderVisual(VolumeVisualizationObject volume)
    {
        // For 2D view, cylinder appears as circle
        var diameter = Math.Max(10, volume.Radius * 2 * 0.25);
        
        // Create a canvas to hold the shape and diagonal lines
        var canvas = new Canvas
        {
            Width = diameter,
            Height = diameter
        };
        
        // Create the ellipse outline
        var ellipse = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = Brushes.Transparent, // No solid fill
            Stroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 0, 0)),
            StrokeThickness = 2
        };
        canvas.Children.Add(ellipse);
        
        // Add diagonal lines for shading (clipped to circle)
        AddDiagonalLinesClipped(canvas, diameter, diameter, true);
        
        return canvas;
    }
    
    private void AddDiagonalLines(Canvas canvas, double width, double height)
    {
        const double lineSpacing = 8; // Spacing between diagonal lines
        var lineColor = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 0, 0));
        
        // Add diagonal lines from top-left to bottom-right
        for (double offset = -height; offset < width + height; offset += lineSpacing)
        {
            var line = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(Math.Max(0, offset), Math.Max(0, -offset)),
                EndPoint = new Avalonia.Point(Math.Min(width, offset + height), Math.Min(height, height - offset)),
                Stroke = lineColor,
                StrokeThickness = 1,
                Opacity = 0.6
            };
            
            canvas.Children.Add(line);
        }
    }
    
    private void AddDiagonalLinesClipped(Canvas canvas, double width, double height, bool isCircle)
    {
        const double lineSpacing = 8; // Spacing between diagonal lines
        var lineColor = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 0, 0));
        
        var centerX = width / 2;
        var centerY = height / 2;
        var radius = Math.Min(width, height) / 2;
        
        // Add diagonal lines, but clip them to the circle if needed
        for (double offset = -height; offset < width + height; offset += lineSpacing)
        {
            var startX = Math.Max(0, offset);
            var startY = Math.Max(0, -offset);
            var endX = Math.Min(width, offset + height);
            var endY = Math.Min(height, height - offset);
            
            if (isCircle)
            {
                // Clip line to circle bounds (simplified approach)
                var line = new Avalonia.Controls.Shapes.Line
                {
                    StartPoint = new Avalonia.Point(startX, startY),
                    EndPoint = new Avalonia.Point(endX, endY),
                    Stroke = lineColor,
                    StrokeThickness = 1,
                    Opacity = 0.6
                };
                
                // Apply a circular clip geometry
                line.Clip = new EllipseGeometry(new Avalonia.Rect(0, 0, width, height));
                canvas.Children.Add(line);
            }
            else
            {
                var line = new Avalonia.Controls.Shapes.Line
                {
                    StartPoint = new Avalonia.Point(startX, startY),
                    EndPoint = new Avalonia.Point(endX, endY),
                    Stroke = lineColor,
                    StrokeThickness = 1,
                    Opacity = 0.6
                };
                
                canvas.Children.Add(line);
            }
        }
    }


    private Control? CreateCollisionShapeVisual(CollisionVisualizationObject collision)
    {
        // Convert coordinates (same as zone objects)
        var canvasX = 10000.0 + (collision.X * 0.25);
        var canvasY = 10000.0 - (collision.Y * 0.25);
        
        // Neon blue color as requested
        var collisionBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(0, 255, 255)); // #00FFFF
        var collisionStroke = new SolidColorBrush(Avalonia.Media.Color.FromRgb(0, 200, 255)); // Slightly darker blue for stroke
        
        Control? visual = collision.GeometryParams switch
        {
            BcdGeomParams.BoxGeomParams box => CreateBoxVisual(box, collision.Scale),
            BcdGeomParams.SphereGeomParams sphere => CreateSphereVisual(sphere, collision.Scale),
            BcdGeomParams.CylinderGeomParams cylinder => CreateCylinderVisual(cylinder, collision.Scale),
            BcdGeomParams.TubeGeomParams tube => CreateTubeVisual(tube, collision.Scale),
            BcdGeomParams.PlaneGeomParams plane => CreatePlaneVisual(plane, collision.Scale),
            BcdGeomParams.MeshGeomParams => CreateMeshVisual(collision.Mesh, collision.Scale),
            BcdGeomParams.RayGeomParams ray => CreateRayVisual(ray, collision.Scale),
            _ => null
        };
        
        if (visual != null)
        {
            // Apply neon blue styling
            if (visual is Shape shape)
            {
                shape.Fill = collisionBrush;
                shape.Stroke = collisionStroke;
                shape.StrokeThickness = 1;
                shape.Opacity = 0.6; // Semi-transparent
            }
            
            // Position on canvas
            Canvas.SetLeft(visual, canvasX);
            Canvas.SetTop(visual, canvasY);
            
            // Apply rotation if needed (simplified 2D rotation)
            ApplyRotationTransform(visual, collision.Rotation);
            
            // Add tooltip
            ToolTip.SetTip(visual, $"{collision.Name} ({collision.GeometryType})\nFlags: {collision.FlagsText}\nAt: ({collision.X:F1}, {collision.Y:F1}, {collision.Z:F1})");
            
            // Tag for identification
            visual.Tag = collision;
        }
        
        return visual;
    }

    private Avalonia.Controls.Shapes.Rectangle CreateBoxVisual(BcdGeomParams.BoxGeomParams box, float scale)
    {
        return new Avalonia.Controls.Shapes.Rectangle
        {
            Width = Math.Max(2, box.Length * scale * 0.25),
            Height = Math.Max(2, box.Width * scale * 0.25)
        };
    }

    private Ellipse CreateSphereVisual(BcdGeomParams.SphereGeomParams sphere, float scale)
    {
        var diameter = Math.Max(4, sphere.Radius * 2 * scale * 0.25);
        return new Ellipse
        {
            Width = diameter,
            Height = diameter
        };
    }

    private Ellipse CreateCylinderVisual(BcdGeomParams.CylinderGeomParams cylinder, float scale)
    {
        // For 2D view, cylinder appears as circle
        var diameter = Math.Max(4, cylinder.Radius * 2 * scale * 0.25);
        return new Ellipse
        {
            Width = diameter,
            Height = diameter
        };
    }

    private Ellipse CreateTubeVisual(BcdGeomParams.TubeGeomParams tube, float scale)
    {
        // For 2D view, tube appears as circle (similar to cylinder)
        var diameter = Math.Max(4, tube.Radius * 2 * scale * 0.25);
        return new Ellipse
        {
            Width = diameter,
            Height = diameter
        };
    }

    private Avalonia.Controls.Shapes.Line CreatePlaneVisual(BcdGeomParams.PlaneGeomParams plane, float scale)
    {
        // Create a line representing the plane intersection with the 2D view
        // Use normal vector to determine line orientation
        var length = 100 * scale * 0.25; // Fixed length for visualization
        
        return new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Avalonia.Point(-length/2, 0),
            EndPoint = new Avalonia.Point(length/2, 0),
            StrokeThickness = 2
        };
    }

    private Avalonia.Controls.Shapes.Line CreateRayVisual(BcdGeomParams.RayGeomParams ray, float scale)
    {
        var length = Math.Max(10, ray.Length * scale * 0.25);
        
        return new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Avalonia.Point(0, 0),
            EndPoint = new Avalonia.Point(length, 0),
            StrokeThickness = 2
        };
    }

    private Polygon? CreateMeshVisual(Imcodec.BCD.ProxyMesh? mesh, float scale)
    {
        if (mesh == null || mesh.Vertices.Count == 0)
            return null;
            
        // Create a simplified polygon from mesh vertices (project to 2D)
        var points = new List<Avalonia.Point>();
        
        foreach (var vertex in mesh.Vertices)
        {
            // Project 3D vertex to 2D (ignore Z for now)
            var x = vertex[0] * scale * 0.25;
            var y = -vertex[1] * scale * 0.25; // Flip Y for canvas
            points.Add(new Avalonia.Point(x, y));
        }
        
        // Limit to reasonable number of points for performance
        if (points.Count > 50)
        {
            points = points.Take(50).ToList();
        }
        
        return new Polygon
        {
            Points = points
        };
    }

    private void ApplyRotationTransform(Control visual, float[,] rotationMatrix)
    {
        // Apply simplified 2D rotation based on the 3D rotation matrix
        // Extract rotation around Z-axis for 2D representation
        var angle = Math.Atan2(rotationMatrix[1, 0], rotationMatrix[0, 0]) * 180.0 / Math.PI;
        
        if (Math.Abs(angle) > 0.1) // Only apply if there's significant rotation
        {
            visual.RenderTransform = new RotateTransform(angle);
        }
    }

    private void SelectObject(ZoneVisualizationObject visualObj)
    {
        ClearAllSelections();
        
        SelectedObject = visualObj;
        
        // Find the corresponding CoreObjectInfo
        if (_currentZoneData?.m_objectList != null)
        {
            SelectedCoreObject = _currentZoneData.m_objectList
                .FirstOrDefault(obj => obj != null && (ulong)obj.m_templateID == visualObj.TemplateID);
        }
        
        NotifySelectionChanged();
    }
    
    private void SelectCollision(CollisionVisualizationObject collision)
    {
        ClearAllSelections();
        SelectedCollision = collision;
        NotifySelectionChanged();
    }
    
    private void SelectPath(PathVisualizationObject path)
    {
        ClearAllSelections();
        SelectedPath = path;
        _selectedHierarchyItem = path;
        this.RaisePropertyChanged(nameof(SelectedHierarchyItem));
        NotifySelectionChanged();
    }
    
    private void SelectMesh(NifMeshVisualizationObject mesh)
    {
        ClearAllSelections();
        SelectedMesh = mesh;
        // Ensure Spawn property notification is explicit
        this.RaisePropertyChanged(nameof(HasSelectedSpawn));
        NotifySelectionChanged();
    }
    
    private void SelectVolume(VolumeVisualizationObject volume)
    {
        ClearAllSelections();
        SelectedVolume = volume;
        // Ensure Spawn property notification is explicit
        this.RaisePropertyChanged(nameof(HasSelectedSpawn));
        NotifySelectionChanged();
    }
    
    private void SelectTrigger(TriggerVisualizationObject trigger)
    {
        ClearAllSelections();
        SelectedTrigger = trigger;
        // Ensure Spawn property notification is explicit
        this.RaisePropertyChanged(nameof(HasSelectedSpawn));
        NotifySelectionChanged();
    }
    
    private void SelectSpawn(SpawnVisualizationObject spawn)
    {
        ClearAllSelections();
        SelectedSpawn = spawn;
        _selectedHierarchyItem = spawn;
        this.RaisePropertyChanged(nameof(SelectedHierarchyItem));
        NotifySelectionChanged();
    }
    
    private void ClearAllSelections()
    {
        SelectedObject = null;
        SelectedCoreObject = null;
        SelectedTemplate = null;
        SelectedCollision = null;
        SelectedPath = null;
        SelectedMesh = null;
        SelectedVolume = null;
        SelectedTrigger = null;
        SelectedSpawn = null;
        SelectedSpawnTemplate = null;
        SelectedNode = null;

        if (_selectedHierarchyItem != null)
        {
            _selectedHierarchyItem = null;
            this.RaisePropertyChanged(nameof(SelectedHierarchyItem));
        }
    }
    
    // Drop table existence cache
    private HashSet<string> _existingDropTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool _dropTableCacheLoaded = false;
    
    private async Task EnsureDropTableCacheAsync()
    {
        if (_dropTableCacheLoaded) return;
        await RefreshDropTableCacheAsync();
    }

    public async Task RefreshDropTableCacheAsync()
    {
        try
        {
            var dropTableService = new DropTableService();
            var all = await dropTableService.GetAllDropTablesAsync();
            _existingDropTableNames = new HashSet<string>(all.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
            _dropTableCacheLoaded = true;
            
            // Refresh UI for loot table item lists
            this.RaisePropertyChanged(nameof(SelectedTemplateLootTableItems));
            this.RaisePropertyChanged(nameof(SelectedSpawnTemplateLootTableItems));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Failed to refresh drop table cache: {ex.Message}");
        }
    }
    
    private void NotifySelectionChanged()
    {
        // Notify UI of property changes for all transform properties
        this.RaisePropertyChanged(nameof(HasSelectedObject));
        this.RaisePropertyChanged(nameof(HasSelectedCollision));
        this.RaisePropertyChanged(nameof(HasSelectedPath));
        this.RaisePropertyChanged(nameof(HasSelectedMesh));
        this.RaisePropertyChanged(nameof(HasSelectedVolume));
        this.RaisePropertyChanged(nameof(HasSelectedTrigger));
        this.RaisePropertyChanged(nameof(HasSelectedSpawn));
        this.RaisePropertyChanged(nameof(HasSelectedNode));
        this.RaisePropertyChanged(nameof(HasSelectedAnyObject));
        
        // Notify path detail properties when path selection changes
        this.RaisePropertyChanged(nameof(SelectedPathName));
        this.RaisePropertyChanged(nameof(SelectedPathId));
        this.RaisePropertyChanged(nameof(SelectedPathType));
        this.RaisePropertyChanged(nameof(SelectedPathNodeCount));
        this.RaisePropertyChanged(nameof(SelectedPathSpawnCount));
        this.RaisePropertyChanged(nameof(SelectedPathDescription));
        this.RaisePropertyChanged(nameof(SelectedPathNodes));
        this.RaisePropertyChanged(nameof(SelectedPathSpawns));
        this.RaisePropertyChanged(nameof(SelectedPathAllProperties));
        
        // Node selection details
        this.RaisePropertyChanged(nameof(SelectedNodeName));
        this.RaisePropertyChanged(nameof(SelectedNodeId));
        this.RaisePropertyChanged(nameof(SelectedNodePath));
        this.RaisePropertyChanged(nameof(SelectedNodeIndex));
        this.RaisePropertyChanged(nameof(SelectedNodeLocation));
        this.RaisePropertyChanged(nameof(SelectedNodeAllProperties));
        
        // Notify spawn detail properties when spawn selection changes
        this.RaisePropertyChanged(nameof(SelectedSpawnName));
        this.RaisePropertyChanged(nameof(SelectedSpawnId));
        this.RaisePropertyChanged(nameof(SelectedSpawnTemplateId));
        this.RaisePropertyChanged(nameof(SelectedSpawnType));
        this.RaisePropertyChanged(nameof(SelectedSpawnPath));
        this.RaisePropertyChanged(nameof(SelectedSpawnLocation));
        this.RaisePropertyChanged(nameof(SelectedSpawnChance));
        this.RaisePropertyChanged(nameof(SelectedSpawnTemplate));
        this.RaisePropertyChanged(nameof(SelectedSpawnGameObjectTemplate));
        
        // Notify spawn template detail properties
        this.RaisePropertyChanged(nameof(SelectedSpawnTemplateClassName));
        this.RaisePropertyChanged(nameof(SelectedSpawnTemplateIcon));
        this.RaisePropertyChanged(nameof(SelectedSpawnTemplateLevel));
        this.RaisePropertyChanged(nameof(SelectedSpawnTemplatePrimarySchool));
        this.RaisePropertyChanged(nameof(SelectedSpawnTemplateObjectName));
        this.RaisePropertyChanged(nameof(SelectedSpawnTemplateDisplayName));
        this.RaisePropertyChanged(nameof(SelectedSpawnTemplateDescription));
        this.RaisePropertyChanged(nameof(SelectedSpawnTemplateLootTables));
        this.RaisePropertyChanged(nameof(HasSpawnLootTables));
        
        // Update creature deck detection asynchronously  
        _ = UpdateCreatureDeckDetectionAsync();
        
        this.RaisePropertyChanged(nameof(LocationX));
        this.RaisePropertyChanged(nameof(LocationY));
        this.RaisePropertyChanged(nameof(LocationZ));
        this.RaisePropertyChanged(nameof(OrientationX));
        this.RaisePropertyChanged(nameof(OrientationY));
        this.RaisePropertyChanged(nameof(OrientationZ));
        this.RaisePropertyChanged(nameof(CurrentLocationX));
        this.RaisePropertyChanged(nameof(CurrentLocationY));
        this.RaisePropertyChanged(nameof(CurrentLocationZ));
        this.RaisePropertyChanged(nameof(CurrentScale));
        this.RaisePropertyChanged(nameof(SelectedTemplate));

// Refresh drop table existence on any selection change
        _ = RefreshDropTableCacheAsync();

// Update highlight visuals on selection change
        UpdateVisualSelection();
        // Update off-screen arrow indicator
        UpdateViewportIndicator();
    }
    
    /// <summary>
    /// Loads the template data for the given CoreObjectInfo using the TemplateManifestService
    /// </summary>
    /// <param name="coreObject">The core object to load template for</param>
    /// <returns>The loaded template, or null if not found or failed to load</returns>
    private async Task<PropertyClass?> LoadTemplateAsync(CoreObjectInfo coreObject)
    {
        try
        {
            if (coreObject == null)
                return null;

            // First, ensure template manifest is loaded
            var manifestService = TemplateManifestService.Instance;
            if (!manifestService.IsLoaded)
            {
                // Try to load template manifest from Root.wad
                var templateManifestData = await RootWadService.Instance.GetFileAsync("TemplateManifest.xml");
                if (templateManifestData == null || !templateManifestData.HasValue)
                {
                    Console.WriteLine("Failed to load TemplateManifest.xml from Root.wad");
                    return null;
                }
                
                if (!manifestService.LoadFromFileData(templateManifestData.Value))
                {
                    Console.WriteLine("Failed to parse TemplateManifest.xml");
                    return null;
                }
            }

            // Find the template location by template ID
            var templateLocations = manifestService.GetAllTemplateLocations();
            var templateLocation = templateLocations.FirstOrDefault(t => t.m_id == coreObject.m_templateID);
            
            if (templateLocation == null)
            {
                Console.WriteLine($"Template with ID {coreObject.m_templateID} not found in manifest");
                return null;
            }

            Console.WriteLine($"Found template: {templateLocation.m_filename} for template ID {coreObject.m_templateID}");

            // Load the template file from Root.wad
            var templateData = await RootWadService.Instance.GetFileAsync(templateLocation.m_filename);
            if (templateData == null || !templateData.HasValue)
            {
                Console.WriteLine($"Failed to load template file: {templateLocation.m_filename}");
                return null;
            }

            // Try to deserialize as GameObjectTemplate first, then fallback to PropertyClass
            var serializer = new BindSerializer();
            
            // First try GameObjectTemplate specifically
            if (serializer.Deserialize<GameObjectTemplate>(templateData.Value.ToArray(), 1, out var gameObjectTemplate) && gameObjectTemplate != null)
            {
                Console.WriteLine($"Successfully loaded GameObjectTemplate: {gameObjectTemplate.GetType().Name} for {coreObject.m_zoneTag.OrIfEmpty("Unknown")}");
                return gameObjectTemplate;
            }
            
            // Fallback to generic PropertyClass deserialization
            if (serializer.Deserialize<PropertyClass>(templateData.Value.ToArray(), 1, out var template) && template != null)
            {
                Console.WriteLine($"Successfully loaded template as PropertyClass: {template.GetType().Name} for {coreObject.m_zoneTag.OrIfEmpty("Unknown")}");
                return template;
            }
            
            Console.WriteLine($"Failed to deserialize template from {templateLocation.m_filename}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading template for object {coreObject.m_zoneTag}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads a template for a spawn using its template ID
    /// </summary>
    /// <param name="templateId">The template ID from the spawn data</param>
    /// <returns>The loaded template, or null if not found or failed to load</returns>
    private async Task<PropertyClass?> LoadSpawnTemplateAsync(ulong templateId)
    {
        try
        {
            // First, ensure template manifest is loaded
            var manifestService = TemplateManifestService.Instance;
            if (!manifestService.IsLoaded)
            {
                var templateManifestData = await RootWadService.Instance.GetFileAsync("TemplateManifest.xml");
                if (templateManifestData == null || !templateManifestData.HasValue)
                {
                    Console.WriteLine("Failed to load TemplateManifest.xml from Root.wad");
                    return null;
                }
                
                if (!manifestService.LoadFromFileData(templateManifestData.Value))
                {
                    Console.WriteLine("Failed to parse TemplateManifest.xml");
                    return null;
                }
            }
            
            // Find the template location by template ID
            var templateLocations = manifestService.GetAllTemplateLocations();
            var templateLocation = templateLocations.FirstOrDefault(t => t.m_id == templateId);
            
            if (templateLocation == null)
            {
                Console.WriteLine($"Spawn template with ID {templateId} not found in manifest");
                return null;
            }
            
            Console.WriteLine($"Loading spawn template: {templateLocation.m_filename} for template ID {templateId}");
            
            // Load the template file from Root.wad
            var templateData = await RootWadService.Instance.GetFileAsync(templateLocation.m_filename);
            if (templateData == null || !templateData.HasValue)
            {
                Console.WriteLine($"Failed to load template file '{templateLocation.m_filename}' from Root.wad");
                return null;
            }
            
            // Use the same BindSerializer instance and configuration as zone data loading
            var bindSerializer = new BindSerializer();
            var templateDataBytes = templateData.Value.ToArray();
            
            // Try GameObjectTemplate first, then fallback to PropertyClass
            if (bindSerializer.Deserialize<GameObjectTemplate>(templateDataBytes, 1, out var gameObjectTemplate) && gameObjectTemplate != null)
            {
                Console.WriteLine($"Successfully loaded spawn GameObjectTemplate: {templateLocation.m_filename}");
                return gameObjectTemplate;
            }
            
            if (bindSerializer.Deserialize<PropertyClass>(templateDataBytes, 1, out var template) && template != null)
            {
                Console.WriteLine($"Successfully loaded spawn template as PropertyClass: {templateLocation.m_filename}");
                return template;
            }
            
            Console.WriteLine($"Failed to deserialize spawn template from '{templateLocation.m_filename}'");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading spawn template ID {templateId}: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Loads templates for all spawns asynchronously and updates their CreatureType
    /// </summary>
    private async Task LoadSpawnTemplatesAsync()
    {
        if (SpawnVisualizationObjects.Count == 0)
            return;
            
        Console.WriteLine($"Loading templates for {SpawnVisualizationObjects.Count} spawns...");
        
        var loadedCount = 0;
        var failedCount = 0;
        
        foreach (var spawn in SpawnVisualizationObjects)
        {
            try
            {
                var template = await LoadSpawnTemplateAsync(spawn.TemplateId);
                if (template != null)
                {
                    // Store the template and derive display properties for per-spawn UI
                    spawn.Template = template;
                    spawn.CreatureType = DetermineCreatureType(template);
                    
                    // Populate template-derived fields for binding
                    spawn.TemplateClassName = GetTemplateProperty(template, "m_className");
                    spawn.TemplateIcon = GetTemplateProperty(template, "m_icon");
                    spawn.TemplateLevel = GetTemplateProperty(template, "m_level");
                    spawn.TemplatePrimarySchool = GetTemplateProperty(template, "m_primarySchool");
                    spawn.TemplateObjectName = GetTemplateProperty(template, "m_objectName");
                    spawn.TemplateDisplayName = GetTemplateProperty(template, "m_displayName");
                    spawn.TemplateDescription = GetTemplateProperty(template, "m_description");
                    
                    // Loot tables
                    spawn.LootTables = GetLootTablesFromTemplate(template);
                    
                    loadedCount++;
                    
                    if (loadedCount <= 5) // Log first few for debugging
                    {
                        Console.WriteLine($"  Loaded template for '{spawn.Name}': {spawn.CreatureType}");
                    }
                }
                else
                {
                    spawn.Template = null;
                    spawn.CreatureType = "Unknown Template";
                    spawn.LootTables = null;
                    failedCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading template for spawn '{spawn.Name}': {ex.Message}");
                spawn.Template = null;
                spawn.CreatureType = "Load Error";
                spawn.LootTables = null;
                failedCount++;
            }
        }
        
        Console.WriteLine($"Spawn template loading complete: {loadedCount} loaded, {failedCount} failed");
        
        // Notify UI that spawn data has been updated
        this.RaisePropertyChanged(nameof(SpawnVisualizationObjects));
        this.RaisePropertyChanged(nameof(SelectedPathSpawns));
        this.RaisePropertyChanged(nameof(SelectedPathDescription));
    }
    
    /// <summary>
    /// Determines the creature type from a loaded template
    /// </summary>
    private string DetermineCreatureType(PropertyClass template)
    {
        // Try to get a meaningful creature type from the template
        // This follows similar logic to zone objects
        
        if (template is GameObjectTemplate gameObjectTemplate)
        {
            // Check for specific creature properties
            var displayName = gameObjectTemplate.m_displayName.ToString();
            var objectName = gameObjectTemplate.m_objectName.ToString();
            
            if (!string.IsNullOrEmpty(displayName))
                return $"Creature: {displayName}";
            else if (!string.IsNullOrEmpty(objectName))
                return $"Creature: {objectName}";
            else
                return "Creature";
        }
        
        // For other template types, try to infer from class name
        var templateType = template.GetType().Name;
        if (templateType.Contains("NPC", StringComparison.OrdinalIgnoreCase))
            return "NPC";
        else if (templateType.Contains("Creature", StringComparison.OrdinalIgnoreCase))
            return "Creature";
        else if (templateType.Contains("Monster", StringComparison.OrdinalIgnoreCase))
            return "Monster";
        else
            return templateType.Replace("Template", "").Replace("Object", "");
    }

    private string DetermineObjectType(CoreObjectInfo obj)
    {
        var name = obj.m_zoneTag.ToString().ToLower();

        // Use Dragon tool logic: Check if it's a volume/trigger first
        if (IsVolume(obj)) return "Volume";
        
        // Check for professors
        if (IsProfessor(obj)) return "Professor";
        
        // Check for generic NPCs
        if (IsNPC(obj)) return "NPC";
        
        // Check for buildings
        if (name.Contains("building") || name.Contains("house") || name.Contains("school")) return "Building";
        
        return "Object";
    }
    
    private string DetermineSpawnType(SpawnObjectInfo spawn)
    {
        var name = spawn.m_zoneTag.ToString().ToLower();
        
        // Basic spawn type detection
        if (name.Contains("mob") || name.Contains("enemy") || name.Contains("creature"))
            return "Mob";
        if (name.Contains("npc") || name.Contains("character"))
            return "NPC";
        if (name.Contains("boss") || name.Contains("elite"))
            return "Boss";
        if (name.Contains("pet") || name.Contains("minion"))
            return "Pet";
            
        return "Unknown";
    }
    
    /// <summary>
    /// Safely gets a property value from a template using reflection
    /// </summary>
    private string? GetTemplateProperty(PropertyClass? template, string propertyName)
    {
        if (template == null) return null;
        
        try
        {
            var property = template.GetType().GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(template);
                return value?.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to get property '{propertyName}' from template: {ex.Message}");
        }
        
        return null;
    }

    private bool IsVolume(CoreObjectInfo obj)
    {
        // Volumes are objects that create collision/trigger areas
        var name = obj.m_zoneTag.ToString().ToLower();
        return name.Contains("volume") || 
               name.Contains("trigger") || 
               name.Contains("area") ||
               name.Contains("zone") ||
               name.Contains("teleport") ||
               name.Contains("portal");
    }


    private bool IsProfessor(CoreObjectInfo obj)
    {
        var name = obj.m_zoneTag.ToString().ToLower();
        return name.Contains("professor") || 
               name.Contains("teacher") || 
               name.Contains("trainer") ||
               name.Contains("instructor");
    }

    private bool IsNPC(CoreObjectInfo obj)
    {
        var name = obj.m_zoneTag.ToString().ToLower();
        // Generic NPC detection - any object with a character-like name that isn't a professor
        return !string.IsNullOrEmpty(name) && 
               !IsProfessor(obj) && 
               !IsVolume(obj) &&
               (name.Contains("character") || 
                name.Contains("guard") || 
                name.Contains("citizen") ||
                char.IsUpper(name[0])); // Names starting with uppercase are likely NPCs
    }
    
    /// <summary>
    /// Resolves a locale string reference (e.g., "WC-NPCs_00002516") to its English text.
    /// </summary>
    /// <param name="localeReference">The locale reference string.</param>
    /// <returns>The resolved English text, or null if not found.</returns>
    private string? ResolveLocaleString(string localeReference)
    {
        if (string.IsNullOrEmpty(localeReference) || !LocaleService.Instance.IsLoaded)
        {
            return null;
        }

        // Split the locale reference into category and key (e.g., "WC-NPCs_00002516")
        var underscoreIndex = localeReference.LastIndexOf('_');
        if (underscoreIndex == -1)
        {
            return null; // Invalid format
        }

        var category = localeReference.Substring(0, underscoreIndex);
        var key = localeReference.Substring(underscoreIndex + 1);

        // Pad the key to 8 digits if it's not already and is all numeric
        if (key.Length < 8 && key.All(char.IsDigit))
        {
            key = key.PadLeft(8, '0');
        }

        return LocaleService.Instance.GetString(category, key);
    }
    
    private IBrush GetObjectColorBrush(string objectType)
    {
        return objectType switch
        {
            "NPC" => new SolidColorBrush(Avalonia.Media.Color.FromRgb(100, 150, 255)),      // Light blue
            "Shopkeeper" => new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 200, 50)), // Gold
            "Professor" => new SolidColorBrush(Avalonia.Media.Color.FromRgb(150, 255, 150)), // Light green
            "Building" => new SolidColorBrush(Avalonia.Media.Color.FromRgb(150, 100, 50)),   // Brown
            "Volume" => new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 100, 100)),    // Red
            "Object" => new SolidColorBrush(Avalonia.Media.Color.FromRgb(150, 150, 150)),    // Gray
            _ => new SolidColorBrush(Avalonia.Media.Color.FromRgb(100, 100, 100))            // Dark gray
        };
    }
    
    private string GetObjectIcon(string objectType)
    {
        return objectType switch
        {
            "NPC" => "N",
            "Shopkeeper" => "$",
            "Professor" => "P",
            "Building" => "■",
            "Volume" => "○",
            "Object" => "•",
            _ => "?"
        };
    }
    
    /// <summary>
    /// Updates shopkeeper flags for all NPCs in the zone based on database inventory data
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    private async Task UpdateShopkeeperFlagsFromDatabase()
    {
        try
        {
            Console.WriteLine("[DEBUG] Starting shopkeeper flag update from database...");
            
            // Get all NPC template IDs that have inventory data
            var npcTemplatesWithInventories = await NpcInventoryService.GetNpcTemplateIdsWithInventoriesAsync();
            
            if (npcTemplatesWithInventories.Count == 0)
            {
                Console.WriteLine("[DEBUG] No NPC inventories found in database");
                return;
            }
            
            Console.WriteLine($"[DEBUG] Found {npcTemplatesWithInventories.Count} NPCs with inventory data");
            
            // Update flags for visualization objects
            var updatedCount = 0;
            foreach (var visualObj in ZoneVisualizationObjects)
            {
                // Check if this object is an NPC (has NPC flag)
                var hasNpcFlag = visualObj.Flags.Any(f => f.FlagType == "NPC");
                if (hasNpcFlag)
                {
                    // Check if this NPC has inventory data
                    var hasInventory = npcTemplatesWithInventories.Contains(visualObj.TemplateID);
                    var hasShopkeeperFlag = visualObj.Flags.Any(f => f.FlagType == "Shopkeeper");
                    
                    if (hasInventory && !hasShopkeeperFlag)
                    {
                        // Add shopkeeper flag
                        visualObj.Flags.Add(ObjectFlag.CreateShopkeeperFlag());
                        updatedCount++;
                        Console.WriteLine($"[DEBUG] Added shopkeeper flag to NPC {visualObj.Name} (Template ID: {visualObj.TemplateID})");
                    }
                    else if (!hasInventory && hasShopkeeperFlag)
                    {
                        // Remove shopkeeper flag (inventory was deleted)
                        visualObj.Flags.RemoveAll(f => f.FlagType == "Shopkeeper");
                        updatedCount++;
                        Console.WriteLine($"[DEBUG] Removed shopkeeper flag from NPC {visualObj.Name} (Template ID: {visualObj.TemplateID})");
                    }
                }
            }
            
            Console.WriteLine($"[DEBUG] Updated shopkeeper flags for {updatedCount} objects");
            
            // Refresh shopkeepers collection based on updated flags
            await RefreshShopkeepersCollection();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to update shopkeeper flags from database: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Refreshes the Shopkeepers collection based on current flags and database inventory data
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    private async Task RefreshShopkeepersCollection()
    {
        try
        {
            var tempShopkeepers = new List<ShopkeeperItem>();
            
            // Find all objects with shopkeeper flags
            foreach (var visualObj in ZoneVisualizationObjects)
            {
                var hasShopkeeperFlag = visualObj.Flags.Any(f => f.FlagType == "Shopkeeper");
                if (hasShopkeeperFlag)
                {
                    // Get inventory count from database
                    var inventory = await NpcInventoryService.GetNpcInventoryAsync(visualObj.TemplateID);
                    var inventoryCount = inventory?.Inventory.Count ?? 0;
                    
                    var shopkeeper = new ShopkeeperItem
                    {
                        Name = visualObj.Name,
                        TemplateID = visualObj.TemplateID,
                        InventoryCount = inventoryCount
                    };
                    tempShopkeepers.Add(shopkeeper);
                }
            }
            
            // Update UI collection on UI thread
            Shopkeepers.Clear();
            foreach (var shopkeeper in tempShopkeepers)
            {
                Shopkeepers.Add(shopkeeper);
            }
            
            Console.WriteLine($"[DEBUG] Refreshed shopkeepers collection with {tempShopkeepers.Count} entries");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to refresh shopkeepers collection: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Updates the shopkeeper flag for a specific NPC after inventory changes
    /// </summary>
    /// <param name="templateId">The template ID of the NPC to update</param>
    /// <returns>Task representing the async operation</returns>
    public async Task UpdateShopkeeperFlagForNpc(ulong templateId)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Updating shopkeeper flag for NPC template ID: {templateId}");
            
            // Check if this NPC has inventory data
            var hasInventory = await NpcInventoryService.HasNpcInventoryAsync(templateId);
            
            // Find the visualization object for this template ID
            var visualObj = ZoneVisualizationObjects.FirstOrDefault(v => v.TemplateID == templateId);
            if (visualObj == null)
            {
                Console.WriteLine($"[DEBUG] No visualization object found for template ID: {templateId}");
                return;
            }
            
            // Check if this object has NPC flag (only NPCs can be shopkeepers)
            var hasNpcFlag = visualObj.Flags.Any(f => f.FlagType == "NPC");
            if (!hasNpcFlag)
            {
                Console.WriteLine($"[DEBUG] Object {visualObj.Name} is not an NPC, skipping shopkeeper flag update");
                return;
            }
            
            var hasShopkeeperFlag = visualObj.Flags.Any(f => f.FlagType == "Shopkeeper");
            
            if (hasInventory && !hasShopkeeperFlag)
            {
                // Add shopkeeper flag
                visualObj.Flags.Add(ObjectFlag.CreateShopkeeperFlag());
                Console.WriteLine($"[DEBUG] Added shopkeeper flag to NPC {visualObj.Name}");
            }
            else if (!hasInventory && hasShopkeeperFlag)
            {
                // Remove shopkeeper flag
                visualObj.Flags.RemoveAll(f => f.FlagType == "Shopkeeper");
                Console.WriteLine($"[DEBUG] Removed shopkeeper flag from NPC {visualObj.Name}");
            }
            
            // Refresh shopkeepers collection
            await RefreshShopkeepersCollection();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to update shopkeeper flag for NPC {templateId}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Updates professor flags for all NPCs in the zone based on database spell inventory data
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    private async Task UpdateProfessorFlagsFromDatabase()
    {
        try
        {
            Console.WriteLine("[DEBUG] Starting professor flag update from database...");
            
            // Get all NPC template IDs that have spell inventory data
            var npcTemplatesWithSpellInventories = await NpcSpellInventoryService.GetNpcTemplateIdsWithSpellInventoriesAsync();
            
            Console.WriteLine($"[DEBUG] Retrieved {npcTemplatesWithSpellInventories.Count} template IDs with spell inventories from database");
            if (npcTemplatesWithSpellInventories.Count == 0)
            {
                Console.WriteLine("[DEBUG] No NPC spell inventories found in database");
                return;
            }
            
            Console.WriteLine($"[DEBUG] Found {npcTemplatesWithSpellInventories.Count} NPCs with spell inventory data");
            
            // Update flags for visualization objects
            var updatedCount = 0;
            foreach (var visualObj in ZoneVisualizationObjects)
            {
                // Check if this object is an NPC (has NPC flag)
                var hasNpcFlag = visualObj.Flags.Any(f => f.FlagType == "NPC");
                if (hasNpcFlag)
                {
                    // Check if this NPC has spell inventory data
                    var hasSpellInventory = npcTemplatesWithSpellInventories.Contains(visualObj.TemplateID);
                    var hasProfessorFlag = visualObj.Flags.Any(f => f.FlagType == "Professor");
                    
                    if (hasSpellInventory && !hasProfessorFlag)
                    {
                        // Add professor flag
                        visualObj.Flags.Add(ObjectFlag.CreateProfessorFlag());
                        updatedCount++;
                        Console.WriteLine($"[DEBUG] Added professor flag to NPC {visualObj.Name} (Template ID: {visualObj.TemplateID})");
                    }
                    else if (!hasSpellInventory && hasProfessorFlag)
                    {
                        // Remove professor flag (spell inventory was deleted)
                        visualObj.Flags.RemoveAll(f => f.FlagType == "Professor");
                        updatedCount++;
                        Console.WriteLine($"[DEBUG] Removed professor flag from NPC {visualObj.Name} (Template ID: {visualObj.TemplateID})");
                    }
                }
            }
            
            Console.WriteLine($"[DEBUG] Updated professor flags for {updatedCount} objects");
            
            // Refresh professors collection based on updated flags
            await RefreshProfessorsCollection();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to update professor flags from database: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Refreshes the Professors collection based on current flags and database spell inventory data
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    private async Task RefreshProfessorsCollection()
    {
        try
        {
            var tempProfessors = new List<ProfessorItem>();
            
            // Find all objects with professor flags
            foreach (var visualObj in ZoneVisualizationObjects)
            {
                var hasProfessorFlag = visualObj.Flags.Any(f => f.FlagType == "Professor");
                if (hasProfessorFlag)
                {
                    // Get spell count from database
                    var spellInventory = await NpcSpellInventoryService.GetNpcSpellInventoryAsync(visualObj.TemplateID);
                    var spellCount = spellInventory?.Spells.Count ?? 0;
                    
                    var professor = new ProfessorItem
                    {
                        Name = visualObj.Name,
                        TemplateID = visualObj.TemplateID,
                        SpellCount = spellCount
                    };
                    tempProfessors.Add(professor);
                }
            }
            
            // Update UI collection on UI thread
            Professors.Clear();
            foreach (var professor in tempProfessors)
            {
                Professors.Add(professor);
            }
            
            Console.WriteLine($"[DEBUG] Refreshed professors collection with {tempProfessors.Count} entries");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to refresh professors collection: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Updates the professor flag for a specific NPC after spell inventory changes
    /// </summary>
    /// <param name="templateId">The template ID of the NPC to update</param>
    /// <returns>Task representing the async operation</returns>
    public async Task UpdateProfessorFlagForNpc(ulong templateId)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Updating professor flag for NPC template ID: {templateId}");
            
            // Check if this NPC has spell inventory data
            var hasSpellInventory = await NpcSpellInventoryService.HasNpcSpellInventoryAsync(templateId);
            
            // Find the visualization object for this template ID
            var visualObj = ZoneVisualizationObjects.FirstOrDefault(v => v.TemplateID == templateId);
            if (visualObj == null)
            {
                Console.WriteLine($"[DEBUG] No visualization object found for template ID: {templateId}");
                return;
            }
            
            // Check if this object has NPC flag (only NPCs can be professors)
            var hasNpcFlag = visualObj.Flags.Any(f => f.FlagType == "NPC");
            if (!hasNpcFlag)
            {
                Console.WriteLine($"[DEBUG] Object {visualObj.Name} is not an NPC, skipping professor flag update");
                return;
            }
            
            var hasProfessorFlag = visualObj.Flags.Any(f => f.FlagType == "Professor");
            
            if (hasSpellInventory && !hasProfessorFlag)
            {
                // Add professor flag
                visualObj.Flags.Add(ObjectFlag.CreateProfessorFlag());
                Console.WriteLine($"[DEBUG] Added professor flag to NPC {visualObj.Name}");
            }
            else if (!hasSpellInventory && hasProfessorFlag)
            {
                // Remove professor flag
                visualObj.Flags.RemoveAll(f => f.FlagType == "Professor");
                Console.WriteLine($"[DEBUG] Removed professor flag from NPC {visualObj.Name}");
            }
            
            // Refresh professors collection
            await RefreshProfessorsCollection();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to update professor flag for NPC {templateId}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Gets the loot tables from a template if it has an m_lootTable property.
    /// Uses reflection to detect and read the property dynamically.
    /// </summary>
    /// <param name="template">The template to inspect</param>
    /// <returns>List of loot table names, or null if no loot tables found</returns>
    private List<string>? GetLootTablesFromTemplate(PropertyClass? template)
    {
        if (template == null)
            return null;
            
        try
        {
            // Use reflection to find m_lootTable property
            var lootTableProperty = template.GetType().GetProperty("m_lootTable", BindingFlags.Public | BindingFlags.Instance);
            
            if (lootTableProperty == null)
                return null;
                
            // Get the value
            var lootTableValue = lootTableProperty.GetValue(template);
            
            if (lootTableValue == null)
                return null;
                
            // Check if it's a list of ByteString (as seen in generated code)
            if (lootTableValue is IEnumerable<object> lootTableList)
            {
                var lootTableNames = new List<string>();
                
                foreach (var item in lootTableList)
                {
                    if (item != null)
                    {
                        // Convert to string - ByteString should have ToString() method
                        var tableName = item.ToString();
                        if (!string.IsNullOrWhiteSpace(tableName))
                        {
                            lootTableNames.Add(tableName);
                        }
                    }
                }
                
                return lootTableNames.Count > 0 ? lootTableNames : null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error getting loot tables from template: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Command handler to view an existing drop table in the drop table editor.
    /// </summary>
    /// <param name="dropTableName">The name of the drop table to view</param>
    private async void ViewDropTable(string dropTableName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dropTableName))
            {
                MessageService.Error("Drop table name is empty.").Send();
                return;
            }
            
            Console.WriteLine($"[DEBUG] Opening drop table editor for: {dropTableName}");
            
            var isConfigured = await DataPack.IsLoadedAsync();
            
            if (!isConfigured)
            {
                MessageService.Info("Data pack configuration required to edit drop tables.")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
                return;
            }
            
            // Check if drop table editor tab already exists, otherwise create one
            var tabManager = _mainViewModel.TabManager;
            var existingTab = tabManager.FindTabByContent<DropTableEditorViewModel>();
            
            DropTableEditorViewModel dropTableEditorViewModel;
            
            if (existingTab != null)
            {
                // Select the existing tab
                tabManager.SelectTab(existingTab);
                dropTableEditorViewModel = (DropTableEditorViewModel)existingTab.Content;
            }
            else
            {
                // Create a new drop table editor tab
                dropTableEditorViewModel = new DropTableEditorViewModel(_mainViewModel);
                var tab = tabManager.AddTab("Drop Table Editor", dropTableEditorViewModel);
                tabManager.SelectTab(tab);
            }
            
            // Now try to select the specific drop table in the editor
            // We need to wait for the drop tables to load first
            await Task.Delay(500); // Give the editor time to load
            
            var targetTable = dropTableEditorViewModel.DropTables.FirstOrDefault(t => t.Name == dropTableName);
            if (targetTable != null)
            {
                dropTableEditorViewModel.SelectedDropTable = targetTable;
                MessageService.Info($"Opened drop table '{dropTableName}' in editor.").Send();
            }
            else
            {
                MessageService.Error($"Drop table '{dropTableName}' not found. It may not exist or failed to load.").Send();
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Error opening drop table '{dropTableName}': {ex.Message}").Send();
            Console.WriteLine($"[ERROR] ViewDropTable failed: {ex}");
        }
    }
    
    /// <summary>
    /// Command handler to create a new drop table with the given name.
    /// </summary>
    /// <param name="dropTableName">The name of the drop table to create</param>
    private async void CreateDropTable(string dropTableName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dropTableName))
            {
                MessageService.Error("Drop table name is empty.").Send();
                return;
            }
            
            Console.WriteLine($"[DEBUG] Creating new drop table: {dropTableName}");
            
            var isConfigured = await DataPack.IsLoadedAsync();
            
            if (!isConfigured)
            {
                MessageService.Info("Data pack configuration required to create drop tables.")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
                return;
            }
            
            // Check if drop table editor tab already exists, otherwise create one
            var tabManager = _mainViewModel.TabManager;
            var existingTab = tabManager.FindTabByContent<DropTableEditorViewModel>();
            
            DropTableEditorViewModel dropTableEditorViewModel;
            
            if (existingTab != null)
            {
                // Select the existing tab
                tabManager.SelectTab(existingTab);
                dropTableEditorViewModel = (DropTableEditorViewModel)existingTab.Content;
            }
            else
            {
                // Create a new drop table editor tab
                dropTableEditorViewModel = new DropTableEditorViewModel(_mainViewModel);
                var tab = tabManager.AddTab("Drop Table Editor", dropTableEditorViewModel);
                tabManager.SelectTab(tab);
            }
            
            // Check if drop table already exists
            var dropTableService = new DropTableService();
            var existingTables = await dropTableService.GetAllDropTablesAsync();
            var existingTable = existingTables.FirstOrDefault(t => t.Name.Equals(dropTableName, StringComparison.OrdinalIgnoreCase));
            
            if (existingTable != null)
            {
                // Select the existing table
                await Task.Delay(500); // Give the editor time to load
                var targetTable = dropTableEditorViewModel.DropTables.FirstOrDefault(t => t.Name == dropTableName);
                if (targetTable != null)
                {
                    dropTableEditorViewModel.SelectedDropTable = targetTable;
                    MessageService.Info($"Drop table '{dropTableName}' already exists. Opened existing table in editor.").Send();
                }
                return;
            }
            
            // Create new drop table via the DropTableEditorViewModel
            // Trigger the create table dialog which will handle the creation
            MessageService.Info($"Opening drop table editor to create '{dropTableName}'. Use 'Create New Table' to create it.").Send();
        }
        catch (Exception ex)
        {
            MessageService.Error($"Error creating drop table '{dropTableName}': {ex.Message}").Send();
            Console.WriteLine($"[ERROR] CreateDropTable failed: {ex}");
        }
    }
    
    private async void OpenOrCreateDropTable(string dropTableName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dropTableName))
            {
                MessageService.Error("Drop table name is empty.").Send();
                return;
            }
            
            var isConfigured = await DataPack.IsLoadedAsync();
            if (!isConfigured)
            {
                MessageService.Info("Data pack configuration required to open or create drop tables.")
                    .WithDuration(TimeSpan.FromSeconds(3))
                    .Send();
                return;
            }
            
            var dropTableService = new DropTableService();
            var existing = await dropTableService.GetDropTableAsync(dropTableName);
            
            // Open editor tab
            var tabManager = _mainViewModel.TabManager;
            var existingTab = tabManager.FindTabByContent<DropTableEditorViewModel>();
            DropTableEditorViewModel vm;
            if (existingTab != null)
            {
                tabManager.SelectTab(existingTab);
                vm = (DropTableEditorViewModel)existingTab.Content;
            }
            else
            {
                vm = new DropTableEditorViewModel(_mainViewModel);
                var tab = tabManager.AddTab("Drop Table Editor", vm);
                tabManager.SelectTab(tab);
            }
            
            await Task.Delay(300);
            
            if (existing != null)
            {
                // Select existing table
                var target = vm.DropTables.FirstOrDefault(t => t.Name.Equals(dropTableName, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    vm.SelectedDropTable = target;
                    MessageService.Info($"Opened existing drop table '{dropTableName}'.").Send();
                }
                else
                {
                    MessageService.Info($"Drop table '{dropTableName}' exists. Use search to locate it.").Send();
                }
            }
            else
            {
                MessageService.Info($"Drop table '{dropTableName}' was not found. Use 'Create New Table' in the editor to create it.").Send();
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"Error opening or creating drop table '{dropTableName}': {ex.Message}").Send();
        }
    }
    
    /// <summary>
    /// Debug method to test spell inventory system - remove this in production
    /// </summary>
    public async Task TestSpellInventorySystem()
    {
        try
        {
            Console.WriteLine("[DEBUG] Testing spell inventory system...");
            
            // Test 1: Try to query database for any existing spell inventories
            var allSpellInventories = await NpcSpellInventoryService.GetAllNpcSpellInventoriesAsync();
            Console.WriteLine($"[DEBUG] Found {allSpellInventories.Count} existing spell inventories in database");
            
            // Test 2: Try to get template IDs with spell inventories
            var templateIdsWithSpells = await NpcSpellInventoryService.GetNpcTemplateIdsWithSpellInventoriesAsync();
            Console.WriteLine($"[DEBUG] Template IDs with spells: {templateIdsWithSpells.Count}");
            
            // Test 3: Try to create a test spell inventory
            var testTemplateId = 12345UL;
            var testSpellInventory = new Database.Models.NpcSpellInventory(testTemplateId);
            testSpellInventory.Spells.Add(new Database.Models.SpellInventoryItem(170282421, 0, 50));
            testSpellInventory.Spells.Add(new Database.Models.SpellInventoryItem(1966685517, 0, 50));
            
            Console.WriteLine($"[DEBUG] Attempting to save test spell inventory for template ID {testTemplateId}");
            var saveResult = await NpcSpellInventoryService.SaveNpcSpellInventoryAsync(testSpellInventory);
            Console.WriteLine($"[DEBUG] Save result: {saveResult}");
            
            if (saveResult)
            {
                // Test 4: Try to retrieve the saved inventory
                var retrievedInventory = await NpcSpellInventoryService.GetNpcSpellInventoryAsync(testTemplateId);
                Console.WriteLine($"[DEBUG] Retrieved inventory: {retrievedInventory != null}, spells count: {retrievedInventory?.Spells?.Count ?? 0}");
                
                // Test 5: Clean up - delete the test inventory
                var deleteResult = await NpcSpellInventoryService.DeleteNpcSpellInventoryAsync(testTemplateId);
                Console.WriteLine($"[DEBUG] Delete result: {deleteResult}");
            }
            
            Console.WriteLine("[DEBUG] Spell inventory system test completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Spell inventory system test failed: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
        }
    }
}

public class ZoneObjectItem
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public ulong TemplateID { get; set; }
    public Vector3 Location { get; set; }
    public float Scale { get; set; } = 1.0f;
}

public class ZoneVisualizationObject
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public ulong TemplateID { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Scale { get; set; } = 1.0f;
    public List<ObjectFlag> Flags { get; set; } = new();
    
    public string DisplayText => $"{Name} ({Type})";
    public string CoordinateText => $"({X:F1}, {Y:F1}, {Z:F1})";
}

public class ShopkeeperItem
{
    public string Name { get; set; } = string.Empty;
    public ulong TemplateID { get; set; }
    public int InventoryCount { get; set; }
}

public class ProfessorItem
{
    public string Name { get; set; } = string.Empty;
    public ulong TemplateID { get; set; }
    public int SpellCount { get; set; }
}

public class ZoneTransferItem
{
    public string TriggerName { get; set; } = string.Empty;
    public string DestinationZone { get; set; } = string.Empty;
}

public class CollisionVisualizationObject
{
    public string Name { get; set; } = string.Empty;
    public string GeometryType { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Scale { get; set; } = 1.0f;
    public float[,] Rotation { get; set; } = new float[3, 3];
    public string Material { get; set; } = string.Empty;
    public CollisionFlags CategoryFlags { get; set; }
    public CollisionFlags CollisionFlags { get; set; }
    public BcdGeomParams.GeomParams GeometryParams { get; set; } = new BcdGeomParams.MeshGeomParams();
    public Imcodec.BCD.ProxyMesh? Mesh { get; set; }
    
    public string DisplayText => $"{Name} ({GeometryType})";
    public string CoordinateText => $"({X:F1}, {Y:F1}, {Z:F1})";
    public string FlagsText => $"Cat: {CategoryFlags}, Col: {CollisionFlags}";
}

public class NifMeshVisualizationObject
{
    public string Name { get; set; } = string.Empty;
    public List<Avalonia.Point> Vertices2D { get; set; } = new();
    public List<int[]> Triangles { get; set; } = new();
    public List<Avalonia.Point> BoundingBox { get; set; } = new();
    public int VertexCount { get; set; }
    public int TriangleCount { get; set; }
    
    public string DisplayText => $"{Name} (NIF Mesh)";
    public string GeometryInfo => $"Vertices: {VertexCount}, Triangles: {TriangleCount}";
    public bool HasValidGeometry => Vertices2D.Count > 0;
}

public class SpawnVisualizationObject
{
    public string Name { get; set; } = string.Empty;
    public ulong SpawnId { get; set; }
    public ulong TemplateId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Scale { get; set; } = 1.0f;
    public byte SpawnChance { get; set; } // Percentage chance to spawn (0-100)
    public string SpawnType { get; set; } = string.Empty;
    public ulong PathId { get; set; } // Which path this spawn follows
    public string CreatureType { get; set; } = string.Empty; // Type of creature spawned
    public List<ulong> PathIds { get; set; } = new(); // Paths this spawn uses

    // Loaded template for this spawn (set asynchronously)
    public PropertyClass? Template { get; set; }

    // Template-derived display properties
    public string? TemplateClassName { get; set; }
    public string? TemplateIcon { get; set; }
    public string? TemplateLevel { get; set; }
    public string? TemplatePrimarySchool { get; set; }
    public string? TemplateObjectName { get; set; }
    public string? TemplateDisplayName { get; set; }
    public string? TemplateDescription { get; set; }

    // Loot tables extracted from template
    public List<string>? LootTables { get; set; }
    public bool HasLootTables => LootTables?.Count > 0;
    
    public string DisplayText => $"{Name} (Spawn)";
    public string CoordinateText => $"({X:F1}, {Y:F1}, {Z:F1})";
    public string PathInfo => PathIds.Count > 0 ? $"Paths: {string.Join(", ", PathIds)}" : "No paths";
}

public class NodeVisualizationObject
{
    public ulong NodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public ulong PathId { get; set; } // Which path this node belongs to
    public int NodeIndex { get; set; } // Order in the path

    // Reference to the original deserialized node object for deep inspection
    public object? OriginalNodeObject { get; set; }
    
    public string DisplayText => $"Node {NodeIndex} ({Name})";
    public string CoordinateText => $"({X:F1}, {Y:F1}, {Z:F1})";
}

public class PathVisualizationObject
{
    public ulong PathId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<NodeVisualizationObject> Nodes { get; set; } = new();
    public List<SpawnVisualizationObject> Spawns { get; set; } = new();
    public List<ulong> SpawnIds { get; set; } = new(); // Spawns that use this path
    public string PathType { get; set; } = "Unknown";

    // Reference to the original deserialized path object for deep inspection
    public object? OriginalPathObject { get; set; }
    
    public string DisplayText => $"{Name} (Path)";
    public string NodeInfo => $"Nodes: {Nodes.Count}";
    public string SpawnInfo => SpawnIds.Count > 0 ? $"Used by {SpawnIds.Count} spawn(s)" : "Unused path";
    public bool HasNodes => Nodes.Count > 0;

    // For tree view: nodes first, then spawns
    public List<object> CombinedChildren => Nodes.Cast<object>().Concat(Spawns).ToList();
}

public class LootTableItem
{
    public string Name { get; }
    public bool Exists { get; }
    public string Label => Exists ? "View" : "Create";
    
    public LootTableItem(string name, bool exists)
    {
        Name = name;
        Exists = exists;
    }
}

public class NodePropertyItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

// Helper class for identifying path line components in the visual tree
public class PathLineTag
{
    public PathVisualizationObject? Path { get; set; }
    public bool IsArrow { get; set; }
}

/// <summary>
/// Represents a flag that can be displayed next to objects in the scene hierarchy
/// </summary>
public class ObjectFlag
{
    public string FlagType { get; set; } = string.Empty;
    public string Letter { get; set; } = "?";
    public string Color { get; set; } = "#CCCCCC"; // Default gray
    public string ToolTip { get; set; } = string.Empty;
    
    /// <summary>
    /// Creates a flag for NPC objects
    /// </summary>
    public static ObjectFlag CreateNpcFlag()
    {
        return new ObjectFlag
        {
            FlagType = "NPC",
            Letter = "N",
            Color = "#4CAF50", // Green
            ToolTip = "Non-Player Character (has NPCBehaviorTemplate)"
        };
    }
    
    /// <summary>
    /// Creates a flag for Shopkeeper objects
    /// </summary>
    public static ObjectFlag CreateShopkeeperFlag()
    {
        return new ObjectFlag
        {
            FlagType = "Shopkeeper",
            Letter = "$",
            Color = "#FFD700", // Gold
            ToolTip = "Shopkeeper"
        };
    }
    
    /// <summary>
    /// Creates a flag for Professor objects
    /// </summary>
    public static ObjectFlag CreateProfessorFlag()
    {
        return new ObjectFlag
        {
            FlagType = "Professor",
            Letter = "P",
            Color = "#2196F3", // Blue
            ToolTip = "Professor/Teacher/Trainer"
        };
    }
    
    /// <summary>
    /// Creates a flag for Teleporter objects
    /// </summary>
    public static ObjectFlag CreateTeleporterFlag()
    {
        return new ObjectFlag
        {
            FlagType = "Teleporter",
            Letter = "T",
            Color = "#9C27B0", // Purple
            ToolTip = "Trigger with teleport functionality (has ResTeleport result)"
        };
    }
}

/// <summary>
/// Service for detecting and assigning flags to zone objects based on their templates
/// </summary>
public class ObjectFlagService
{
    /// <summary>
    /// Analyzes a GameObjectTemplate and determines what flags should be applied
    /// </summary>
    /// <param name="template">The GameObjectTemplate to analyze</param>
    /// <param name="coreObject">The CoreObjectInfo for fallback name-based detection</param>
    /// <returns>List of flags that should be applied to this object</returns>
    public static List<ObjectFlag> DetectFlags(GameObjectTemplate? template, CoreObjectInfo coreObject)
    {
        var flags = new List<ObjectFlag>();

        if (template != null)
        {
            // Check for NPCBehaviorTemplate in behaviors
            if (HasNpcBehaviorTemplate(template))
            {
                flags.Add(ObjectFlag.CreateNpcFlag());
            }
        }

        // Fallback to name-based detection for additional flags
        var name = coreObject.m_zoneTag.ToString().ToLower();
        
        // Note: Shopkeeper flags are now only assigned based on database inventory data
        // Name-based shopkeeper detection was removed to avoid false positives
        
        // Note: Professor flags are now only assigned based on database spell inventory data
        // Name-based professor detection was removed to avoid false positives
        
        // Note: Teleporter flags are now only assigned based on actual ResTeleport results in triggers
        // Name-based teleporter detection was removed to avoid false positives on zone objects

        return flags;
    }

    /// <summary>
    /// Analyzes a trigger and determines what flags should be applied
    /// </summary>
    /// <param name="trigger">The trigger to analyze</param>
    /// <returns>List of flags that should be applied to this trigger</returns>
    public static List<ObjectFlag> DetectTriggerFlags(Trigger trigger)
    {
        var flags = new List<ObjectFlag>();

        if (trigger.m_results?.m_results != null)
        {
            // Check for ResTeleport results
            foreach (var result in trigger.m_results.m_results)
            {
                if (result is ResTeleport)
                {
                    flags.Add(ObjectFlag.CreateTeleporterFlag());
                    break; // Only add the flag once even if multiple teleport results exist
                }
            }
        }

        return flags;
    }


    /// <summary>
    /// Checks if a GameObjectTemplate has an NPCBehaviorTemplate behavior
    /// </summary>
    /// <param name="template">The template to check</param>
    /// <returns>True if the template has NPCBehaviorTemplate, false otherwise</returns>
    private static bool HasNpcBehaviorTemplate(GameObjectTemplate template)
    {
        if (template.m_behaviors == null)
            return false;

        foreach (var behavior in template.m_behaviors)
        {
            if (behavior != null && behavior.GetType().Name == "NPCBehaviorTemplate")
            {
                return true;
            }
        }

        return false;
    }
}

public class BehaviorWrapper
{
    public object? OriginalBehavior { get; }
    public string TypeName { get; }
    public string FormattedJson { get; }

    public BehaviorWrapper(object? behavior)
    {
        OriginalBehavior = behavior;

        if (behavior == null)
        {
            TypeName = "Unknown";
            FormattedJson = "// Behavior object is null";
            return;
        }

        TypeName = behavior.GetType().Name;

        // Use DeepJsonSerializer for proper nested object handling
        FormattedJson = DeepJsonSerializer.SerializeDeep(behavior);
    }
}

/// <summary>
/// Database lookup status for ResTeleport results
/// </summary>
public enum TeleportDataStatus
{
    NotApplicable,  // Not a ResTeleport result
    Loading,        // Currently loading from database
    Found,          // Data found in database
    NotFound,       // No data found in database
    Error          // Error occurred during lookup
}

/// <summary>
/// Wrapper for Result objects with JSON serialization and database lookup for ResTeleport
/// </summary>
public class ResultWrapper : ReactiveObject
{
    private Result? _originalResult;
    private string _formattedJson;
    private TeleportDataStatus _teleportStatus = TeleportDataStatus.NotApplicable;
    private bool _canEdit;
    private bool _canCreate;

    public Result? OriginalResult
    {
        get => _originalResult;
        private set => this.RaiseAndSetIfChanged(ref _originalResult, value);
    }

    public string TypeName { get; }

    public string FormattedJson
    {
        get => _formattedJson;
        private set => this.RaiseAndSetIfChanged(ref _formattedJson, value);
    }

    public TeleportDataStatus TeleportStatus
    {
        get => _teleportStatus;
        private set => this.RaiseAndSetIfChanged(ref _teleportStatus, value);
    }

    public bool CanEdit
    {
        get => _canEdit;
        private set => this.RaiseAndSetIfChanged(ref _canEdit, value);
    }

    public bool CanCreate
    {
        get => _canCreate;
        private set => this.RaiseAndSetIfChanged(ref _canCreate, value);
    }

    public string? ZoneName { get; }
    public string? TriggerName { get; }

    public ResultWrapper(Result? result, string? zoneName = null, string? triggerName = null)
    {
        _originalResult = result;
        ZoneName = zoneName;
        TriggerName = triggerName;

        if (result == null)
        {
            TypeName = "Unknown";
            _formattedJson = "// Result object is null";
            return;
        }

        TypeName = result.GetType().Name;
        _formattedJson = DeepJsonSerializer.SerializeDeep(result);

        // Check if this is a ResTeleport for database lookup
        if (result is ResTeleport && !string.IsNullOrEmpty(zoneName) && !string.IsNullOrEmpty(triggerName))
        {
            TeleportStatus = TeleportDataStatus.Loading;
            _ = Task.Run(async () => await LoadTeleportDataAsync());
        }
    }

    private async Task LoadTeleportDataAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(ZoneName) || string.IsNullOrEmpty(TriggerName))
                return;

            var teleportData = await ZoneTransferService.GetTeleportDataAsync(ZoneName, TriggerName);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (teleportData != null)
                {
                    TeleportStatus = TeleportDataStatus.Found;
                    CanEdit = true;
                    CanCreate = false;
                    OriginalResult = teleportData;
                    FormattedJson = DeepJsonSerializer.SerializeDeep(teleportData);
                }
                else
                {
                    TeleportStatus = TeleportDataStatus.NotFound;
                    CanEdit = false;
                    CanCreate = true;
                }
            });
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                TeleportStatus = TeleportDataStatus.Error;
            });
            Console.WriteLine($"[ResultWrapper] Error loading teleport data: {ex.Message}");
        }
    }

    public void RefreshFromDatabase()
    {
        if (OriginalResult is ResTeleport && !string.IsNullOrEmpty(ZoneName) && !string.IsNullOrEmpty(TriggerName))
        {
            TeleportStatus = TeleportDataStatus.Loading;
            _ = Task.Run(async () => await LoadTeleportDataAsync());
        }
    }
}

/// <summary>
/// Wrapper for Requirement objects with JSON serialization
/// </summary>
public class RequirementWrapper
{
    public Requirement? OriginalRequirement { get; }
    public string TypeName { get; }
    public string FormattedJson { get; }

    public RequirementWrapper(Requirement? requirement)
    {
        OriginalRequirement = requirement;

        if (requirement == null)
        {
            TypeName = "Unknown";
            FormattedJson = "// Requirement object is null";
            return;
        }

        TypeName = requirement.GetType().Name;
        FormattedJson = DeepJsonSerializer.SerializeDeep(requirement);
    }
}

/// <summary>
/// Helper class for deep JSON serialization that handles nested objects and collections
/// </summary>
public static class DeepJsonSerializer
{
    public static string SerializeDeep(object obj)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };
            options.Converters.Add(new JsonStringEnumConverter());

            // Convert to a dictionary structure that handles nested objects properly
            var dict = ConvertToDictionary(obj, 0);
            return JsonSerializer.Serialize(dict, options);
        }
        catch
        {
            // Fallback to recursive string-based serialization
            return SerializeObjectRecursive(obj, 0);
        }
    }

    private static object? ConvertToDictionary(object? obj, int depth)
    {
        if (depth > 10 || obj == null)
            return null;

        var type = obj.GetType();

        // Handle primitives and simple types
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type.IsEnum)
            return obj;

        // Handle ByteString - serialize as string representation
        if (type.Name == "ByteString" && type.Namespace == "Imcodec.IO")
            return obj.ToString();

        // Handle collections
        if (obj is System.Collections.ICollection collection)
        {
            var list = new List<object?>();
            foreach (var item in collection)
            {
                list.Add(ConvertToDictionary(item, depth + 1));
            }
            return list;
        }

        // Handle complex objects - convert to dictionary
        var dict = new Dictionary<string, object?>();
        dict["_type"] = type.Name;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && !p.GetIndexParameters().Any());

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(obj);
                dict[prop.Name] = ConvertToDictionary(value, depth + 1);
            }
            catch
            {
                dict[prop.Name] = "<unable to read>";
            }
        }

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            try
            {
                var value = field.GetValue(obj);
                dict[field.Name] = ConvertToDictionary(value, depth + 1);
            }
            catch
            {
                dict[field.Name] = "<unable to read>";
            }
        }

        return dict;
    }

    private static string SerializeObjectRecursive(object obj, int depth)
    {
        if (depth > 10)
            return "\"<max depth reached>\"";

        var type = obj.GetType();
        var indent = new string(' ', depth * 2);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}  \"_type\": \"{type.Name}\",");

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && !p.GetIndexParameters().Any());

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(obj);
                var jsonValue = FormatValueRecursive(value, depth + 1);
                sb.AppendLine($"{indent}  \"{prop.Name}\": {jsonValue},");
            }
            catch
            {
                sb.AppendLine($"{indent}  \"{prop.Name}\": \"<unable to read>\",");
            }
        }

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            try
            {
                var value = field.GetValue(obj);
                var jsonValue = FormatValueRecursive(value, depth + 1);
                sb.AppendLine($"{indent}  \"{field.Name}\": {jsonValue},");
            }
            catch
            {
                sb.AppendLine($"{indent}  \"{field.Name}\": \"<unable to read>\",");
            }
        }

        var result = sb.ToString();
        if (result.EndsWith(",\n"))
        {
            result = result.Substring(0, result.Length - 2) + "\n";
        }
        result += $"{indent}}}";
        return result;
    }

    private static string FormatValueRecursive(object? value, int depth)
    {
        if (depth > 10 || value == null)
            return "null";

        var type = value.GetType();

        // Handle ByteString - serialize as string representation
        if (type.Name == "ByteString" && type.Namespace == "Imcodec.IO")
        {
            var strValue = value.ToString() ?? string.Empty;
            return $"\"{strValue.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r")}\"";
        }

        return value switch
        {
            string s => $"\"{s.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r")}\"",
            bool b => b.ToString().ToLower(),
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => value.ToString() ?? "0",
            Enum e => $"\"{e.ToString()}\"",
            System.Collections.ICollection collection => FormatCollectionRecursive(collection, depth),
            _ => SerializeObjectRecursive(value, depth)
        };
    }

    private static string FormatCollectionRecursive(System.Collections.ICollection collection, int depth)
    {
        if (collection.Count == 0)
            return "[]";

        var indent = new string(' ', depth * 2);
        var innerIndent = new string(' ', (depth + 1) * 2);
        var items = new List<string>();
        foreach (var item in collection)
        {
            items.Add($"{innerIndent}{FormatValueRecursive(item, depth + 1)}");
        }
        return $"[\n{string.Join(",\n", items)}\n{indent}]";
    }
}
