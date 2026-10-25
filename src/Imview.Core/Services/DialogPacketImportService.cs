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
using System.Linq;
using System.Threading.Tasks;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Controls.Templates;
using Imview.PacketReader;
using Imview.PacketReader.Services;

namespace Imview.Core.Services;

/// <summary>
/// Represents a captured dialog entry with metadata for display and import.
/// </summary>
public class CapturedDialogEntry {
    public ulong MobileID { get; set; }
    public ulong QuestID { get; set; }
    public ulong GoalID { get; set; }
    public string CompletionType { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public string PersonaName { get; set; } = string.Empty;
    public DialogEntryWrapper? DialogEntry { get; set; }
    public ActorDialog? ActorDialog { get; set; }

    /// <summary>
    /// Index of this entry within the ActorDialog (0-based).
    /// </summary>
    public int EntryIndex { get; set; }

    /// <summary>
    /// Total number of entries in the ActorDialog.
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// The raw dialog key (locale reference like "WizQst9559_00000129")
    /// </summary>
    public string DialogKey { get; set; } = string.Empty;

    /// <summary>
    /// The resolved English text from the dialog key, or the key itself if not found.
    /// </summary>
    public string ResolvedDialogText { get; set; } = string.Empty;

    /// <summary>
    /// The resolved NPC name from the persona name locale reference.
    /// </summary>
    public string ResolvedPersonaName { get; set; } = string.Empty;

    /// <summary>
    /// Display text for search/filter purposes.
    /// </summary>
    public string SearchText => $"{PersonaName} {Persona} {CompletionType} {DialogKey} {ResolvedDialogText} {ResolvedPersonaName} {QuestID} {GoalID}";

    /// <summary>
    /// Indicates if this is part of a multi-entry ActorDialog.
    /// </summary>
    public bool IsMultiEntry => TotalEntries > 1;
}

/// <summary>
/// Service for importing and managing dialog entries from packet captures.
/// </summary>
public sealed class DialogPacketImportService {

    private static DialogPacketImportService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Singleton instance of the service.
    /// </summary>
    public static DialogPacketImportService Instance {
        get {
            if (_instance == null) {
                lock (_lock) {
                    _instance ??= new DialogPacketImportService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Whether a packet capture has been imported.
    /// </summary>
    public bool HasImportedPacketCapture { get; private set; }

    /// <summary>
    /// The file path of the imported packet capture.
    /// </summary>
    public string? ImportedFilePath { get; private set; }

    /// <summary>
    /// Collection of captured dialog entries.
    /// </summary>
    public ObservableCollection<CapturedDialogEntry> CapturedDialogs { get; } = [];

    /// <summary>
    /// Event raised when a packet capture is imported.
    /// </summary>
    public event EventHandler? PacketCaptureImported;

    /// <summary>
    /// Imports a packet capture file and extracts all MSG_ACTORDIALOG messages.
    /// </summary>
    /// <param name="filePath">Path to the packet capture JSON file.</param>
    /// <returns>True if import was successful.</returns>
    public async Task<bool> ImportPacketCaptureAsync(string filePath) {
        try {
            if (string.IsNullOrEmpty(filePath)) {
                return false;
            }

            // Ensure locale data is loaded for resolving dialog text
            if (!LocaleService.Instance.IsLoaded) {
                MessageService.Info("Loading locale data for dialog text resolution...")
                    .Send();
                Console.WriteLine("[DialogPacketImportService] LocaleService not loaded, attempting to load...");

                // Try to load from Root.wad
                if (RootWadService.Instance.IsLoaded) {
                    Console.WriteLine("[DialogPacketImportService] RootWadService is already loaded, loading locale...");
                    LocaleService.Instance.LoadFromRootWad();
                } else {
                    Console.WriteLine("[DialogPacketImportService] RootWadService not loaded, loading from cache...");
                    var loaded = await RootWadService.Instance.LoadFromCacheAsync();
                    Console.WriteLine($"[DialogPacketImportService] RootWadService.LoadFromCacheAsync result: {loaded}");

                    if (loaded) {
                        // Wait a moment for the background locale load to complete
                        await Task.Delay(500);
                        if (!LocaleService.Instance.IsLoaded) {
                            Console.WriteLine("[DialogPacketImportService] Locale still not loaded, trying synchronously...");
                            LocaleService.Instance.LoadFromRootWad();
                        }
                    }
                }

                Console.WriteLine($"[DialogPacketImportService] LocaleService.IsLoaded after attempts: {LocaleService.Instance.IsLoaded}");

                if (!LocaleService.Instance.IsLoaded) {
                    MessageService.Warn("Locale data not available. Dialog text will show as locale keys. Load Root.wad for full text resolution.")
                        .WithDuration(TimeSpan.FromSeconds(5))
                        .Send();
                } else {
                    var info = LocaleService.Instance.GetInfo();
                    MessageService.Info($"Locale data loaded: {info.CategoryCount} categories, {info.TotalStringCount} strings")
                        .Send();
                }
            }

            // Extract ActorDialogPackets from the capture
            var actorDialogPackets = await PacketReaderService.ExtractPacketsAsync<ActorDialogPacket>(
                filePath,
                "MSG_ACTORDIALOG"
            );

            // Clear existing entries
            CapturedDialogs.Clear();

            // Process each packet
            foreach (var packet in actorDialogPackets) {
                var capturedEntries = ProcessActorDialogPacket(packet);
                if (capturedEntries != null) {
                    foreach (var entry in capturedEntries) {
                        CapturedDialogs.Add(entry);
                    }
                }
            }

            // Update state
            ImportedFilePath = filePath;
            HasImportedPacketCapture = true;

            // Raise event
            PacketCaptureImported?.Invoke(this, EventArgs.Empty);

            MessageService.Info($"Imported {CapturedDialogs.Count} dialog entries from packet capture.")
                .WithDuration(TimeSpan.FromSeconds(3))
                .Send();

            return true;
        }
        catch (Exception ex) {
            MessageService.Error($"Failed to import packet capture: {ex.Message}")
                .WithDuration(TimeSpan.FromSeconds(5))
                .Send();
            return false;
        }
    }

    /// <summary>
    /// Processes an ActorDialogPacket and converts it to a list of CapturedDialogEntry objects.
    /// Each dialog entry in the ActorDialog becomes a separate CapturedDialogEntry.
    /// </summary>
    private List<CapturedDialogEntry>? ProcessActorDialogPacket(ActorDialogPacket packet) {
        try {
            var entries = new List<CapturedDialogEntry>();

            // Resolve the persona name from locale (PersonaName is like "WC-NPCs_00000514")
            var resolvedPersonaName = ResolveLocaleString(packet.PersonaName);

            // Try to deserialize the ActorDialog hex blob
            ActorDialog? actorDialog = null;
            if (!string.IsNullOrEmpty(packet.ActorDialog)) {
                try {
                    var actorDialogBlob = packet.ActorDialog.Replace(" ", string.Empty);
                    var actorDialogBytes = Convert.FromHexString(actorDialogBlob);
                    var serializer = new ObjectSerializer(false, SerializerFlags.None);
                    serializer.Deserialize<ActorDialog>(actorDialogBytes, 16, out actorDialog);
                }
                catch (Exception ex) {
                    Console.WriteLine($"[DialogPacketImportService] Failed to deserialize ActorDialog: {ex.Message}");
                }
            }

            // Create entries from the dialog entries in the ActorDialog
            if (actorDialog?.m_dialogEntries != null && actorDialog.m_dialogEntries.Count > 0) {
                var totalEntries = actorDialog.m_dialogEntries.Count;
                for (int i = 0; i < totalEntries; i++) {
                    var dialogEntry = actorDialog.m_dialogEntries[i];
                    if (dialogEntry is NPCDialogEntry npcEntry) {
                        var entry = new CapturedDialogEntry {
                            MobileID = packet.MobileID,
                            QuestID = packet.QuestID,
                            GoalID = packet.GoalID,
                            CompletionType = packet.CompletionType ?? string.Empty,
                            Persona = packet.Persona ?? string.Empty,
                            PersonaName = packet.PersonaName ?? string.Empty,
                            ActorDialog = actorDialog,
                            DialogEntry = CreateDialogEntryWrapper(npcEntry),
                            DialogKey = npcEntry.m_dialog.ToString(),
                            ResolvedPersonaName = resolvedPersonaName,
                            EntryIndex = i,
                            TotalEntries = totalEntries
                        };

                        // Resolve the dialog text from locale
                        entry.ResolvedDialogText = ResolveLocaleString(entry.DialogKey);

                        entries.Add(entry);
                    }
                }
            }
            else {
                // No dialog entries in the ActorDialog, create a placeholder entry
                var entry = new CapturedDialogEntry {
                    MobileID = packet.MobileID,
                    QuestID = packet.QuestID,
                    GoalID = packet.GoalID,
                    CompletionType = packet.CompletionType ?? string.Empty,
                    Persona = packet.Persona ?? string.Empty,
                    PersonaName = packet.PersonaName ?? string.Empty,
                    ActorDialog = actorDialog,
                    ResolvedPersonaName = resolvedPersonaName
                };
                entries.Add(entry);
            }

            return entries.Count > 0 ? entries : null;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error processing ActorDialogPacket: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Resolves a locale reference to its English text.
    /// </summary>
    /// <param name="localeReference">The locale reference (e.g., "WizQst9559_00000129" or "WC-NPCs_00000514")</param>
    /// <returns>The resolved English text, or the original reference if not found.</returns>
    private static string ResolveLocaleString(string? localeReference) {
        if (string.IsNullOrEmpty(localeReference) || !LocaleService.Instance.IsLoaded) {
            return localeReference ?? "";
        }

        // Split the locale reference into category and key (e.g., "WizQst9559_00000129")
        var underscoreIndex = localeReference.LastIndexOf('_');
        if (underscoreIndex == -1) {
            return localeReference; // Invalid format, return as-is
        }

        var category = localeReference.Substring(0, underscoreIndex);
        var key = localeReference.Substring(underscoreIndex + 1);

        // Pad the key to 8 digits if it's not already and is all numeric
        if (key.Length < 8 && key.All(char.IsDigit)) {
            key = key.PadLeft(8, '0');
        }

        var resolved = LocaleService.Instance.GetString(category, key);
        return !string.IsNullOrEmpty(resolved) ? resolved : localeReference;
    }

    /// <summary>
    /// Creates a DialogEntryWrapper from an NPCDialogEntry.
    /// </summary>
    private static DialogEntryWrapper CreateDialogEntryWrapper(NPCDialogEntry npcEntry) {
        return new DialogEntryWrapper {
            // NPCDialogEntry specific
            PersonaName = npcEntry.m_personaName.ToString(),
            NameOverride = npcEntry.m_nameOverride.ToString(),
            GuiDisplay = npcEntry.m_guiDisplay.ToString(),

            // Core ActorDialogEntry Properties
            DialogKey = npcEntry.m_dialog.ToString(),
            Picture = npcEntry.m_picture.ToString(),
            SoundFile = npcEntry.m_soundFile.ToString(),
            Action = npcEntry.m_action.ToString(),
            DialogEvent = npcEntry.m_dialogEvent.ToString(),
            ActorTemplateID = npcEntry.m_actorTemplateID,
            CameraName = npcEntry.m_cameraName.ToString(),

            // Camera Properties
            InterpolationDuration = npcEntry.m_interpolationDuration,
            CameraOffsetX = npcEntry.m_cameraOffsetX,
            CameraOffsetY = npcEntry.m_cameraOffsetY,
            CameraOffsetZ = npcEntry.m_cameraOffsetZ,
            Pitch = npcEntry.m_pitch,
            Yaw = npcEntry.m_yaw,
            Roll = npcEntry.m_roll,
            CameraShakeType = npcEntry.m_cameraShakeType.ToString(),
            CameraShakeDuration = npcEntry.m_cameraShakeDuration,
            CameraShakeAmplitude = npcEntry.m_cameraShakeAmplitude,

            // Camera Behavior
            BypassCameraOnReview = npcEntry.m_bypassCameraOnReview,
            CameraZoneName = npcEntry.m_cameraZoneName.ToString(),
            Duration = npcEntry.m_duration,
            Delay = npcEntry.m_delay,
            CameraHidePlayers = npcEntry.m_cameraHidePlayers,
            WalkAwayNpcTemplateID = (uint)npcEntry.m_walkAwayNpcTemplateID,

            // Walk Away Properties
            WalkAwayExitDirectionInDegrees = npcEntry.m_walkAwayExitDirectionInDegrees,
            WalkAwayFadeTime = npcEntry.m_walkAwayFadeTime,
            WalkAwayUseCurrentFacing = npcEntry.m_walkAwayUseCurrentFacing,

            // Stand-in and Camera
            StandInPlayerTag = npcEntry.m_standInPlayerTag.ToString(),
            FadeOutCamera = npcEntry.m_fadeOutCamera,
            SnapCameraToPlayerAtExit = npcEntry.m_snapCameraToPlayerAtExit,
            SecondaryCameraName = npcEntry.m_secondaryCameraName.ToString(),
            SecondaryInterpolationDuration = npcEntry.m_secondaryInterpolationDuration,

            // Lists and Arrays
            NpcStandInList = npcEntry.m_npcStandInList?.Select(s => s.ToString()).ToList() ?? [],
            DialogAnimationList = npcEntry.m_dialogAnimationList?.Select(s => s.ToString()).ToList() ?? [],
            DialogTurningList = npcEntry.m_dialogTurningList?.Select(s => s.ToString()).ToList() ?? [],

            // NPC Behavior
            NpcYawOffsetInDegrees = npcEntry.m_npcYawOffsetInDegrees,
            AllowPlayerToMove = npcEntry.m_allowPlayerToMove,

            // Audio Properties
            SoundEffectFile = npcEntry.m_soundEffectFile.ToString(),
            MusicFile = npcEntry.m_musicFile.ToString(),
            NonStackableMusic = npcEntry.m_nonStackableMusic,
            NonRepeatableMusic = npcEntry.m_nonRepeatableMusic,
            PlayMusicAtSFXVolume = npcEntry.m_playMusicAtSFXVolume,
            SoundEffectDelay = npcEntry.m_soundEffectDelay,
            MusicDelay = npcEntry.m_musicDelay,
            MusicFadeTime = npcEntry.m_musicFadeTime,

            // Camera and Dialog Behavior
            DontReleaseCameraAtExit = npcEntry.m_dontReleaseCameraAtExit,
            DisableBackButton = npcEntry.m_disableBackButton,
            EnableExitButton = npcEntry.m_enableExitButton,
            CameraFadeType = npcEntry.m_cameraFadeType.ToString(),
            CameraFadeTime = npcEntry.m_cameraFadeTime,
            IdleAnimation = npcEntry.m_idleAnimation.ToString(),

            // Spam and Music Control
            SpamTime = npcEntry.m_spamTime,
            PlaySoundIfSpamming = npcEntry.m_playSoundIfSpamming,
            PlayMusicIfSpamming = npcEntry.m_playMusicIfSpamming,
            StopMusicFadeTime = npcEntry.m_stopMusicFadeTime,
            RestartMusicFadeTime = npcEntry.m_restartMusicFadeTime,
            MeetsRequirements = npcEntry.m_meetsRequirements,

            // Final Properties
            SecondaryCameraInitialDelay = npcEntry.m_secondaryCameraInitalDelay,
            DisplayButtonsOnTimedDialog = npcEntry.m_displayButtonsOnTimedDialog
        };
    }

    /// <summary>
    /// Clears the imported packet capture data.
    /// </summary>
    public void ClearImportedData() {
        CapturedDialogs.Clear();
        ImportedFilePath = null;
        HasImportedPacketCapture = false;
    }

    /// <summary>
    /// Gets dialog entries filtered by completion type (dialog tag).
    /// </summary>
    /// <param name="dialogTag">The dialog tag to filter by (e.g., "QuestInfo", "Prep", etc.)</param>
    /// <returns>List of matching captured dialog entries.</returns>
    public List<CapturedDialogEntry> GetDialogsByTag(string dialogTag) {
        return [.. CapturedDialogs.Where(d =>
            d.CompletionType?.Equals(dialogTag, StringComparison.OrdinalIgnoreCase) == true)];
    }

    /// <summary>
    /// Checks if a captured entry matches a specific quest by MobileID or QuestID.
    /// </summary>
    public bool MatchesQuest(CapturedDialogEntry entry, ulong? mobileId, ulong? questId) {
        if (mobileId.HasValue && entry.MobileID == mobileId.Value) {
            return true;
        }
        if (questId.HasValue && entry.QuestID == questId.Value) {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if a captured entry matches a specific goal by GoalID.
    /// </summary>
    public bool MatchesGoal(CapturedDialogEntry entry, ulong? goalId) {
        return goalId.HasValue && entry.GoalID == goalId.Value && entry.GoalID != 0;
    }

}