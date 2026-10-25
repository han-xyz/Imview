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

using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imview.PacketReader.Services;

namespace Imview.PacketReader;

public sealed class QuestBuilder {

    private static readonly Dictionary<QuestTemplate, ulong> s_questTemplateIDMap = [];
    private static readonly Dictionary<ulong, GoalTemplate> s_goalIDToTemplateMap = [];
    private static readonly Dictionary<string, ulong> s_questNameToMobileIDMap = [];

    public static async Task<List<QuestTemplate>> BuildQuestsFromPacketCaptureAsync(string packetCapturePath) {
        // Clear static dictionaries for clean state
        s_questTemplateIDMap.Clear();
        s_goalIDToTemplateMap.Clear();
        s_questNameToMobileIDMap.Clear();

        // Try to load template manifest for actor template ID resolution
        // This is optional - if it fails, we'll continue without actor template IDs
        TryLoadTemplateManifest();

        var questOfferPackets = await PacketReaderService.ExtractPacketsAsync<QuestOfferPacket>(
            packetCapturePath,
            "MSG_QUESTOFFER"
        );
        var sendQuestPackets = await PacketReaderService.ExtractPacketsAsync<SendQuestPacket>(
            packetCapturePath,
            "MSG_SENDQUEST"
        );
        var sendGoalPackets = await PacketReaderService.ExtractPacketsAsync<SendGoalPacket>(
            packetCapturePath,
            "MSG_SENDGOAL"
        );
        var actorDialogPackets = await PacketReaderService.ExtractPacketsAsync<ActorDialogPacket>(
            packetCapturePath,
            "MSG_ACTORDIALOG"
        );

        var templates = questOfferPackets
            .Select(ConvertOfferPacketToTemplate)
            .ToList();
        foreach (var template in templates) {
            AddQuestIDToQuestTemplate(template, sendQuestPackets);
            AddGoalsToQuestTemplate(template, sendGoalPackets);
            AddPrepDialogToQuestTemplate(template, actorDialogPackets);
            AddCompletionDialogToQuestTemplate(template, actorDialogPackets);
        }
        
        // Process goal-specific dialogs after all goals have been created and mapped
        AddDialogToGoals(actorDialogPackets);

        return templates;
    }

    private static QuestTemplate ConvertOfferPacketToTemplate(QuestOfferPacket packet) {
        // Craft the initial template.
        var template = new QuestTemplate {
            m_questName = packet.QuestName,
            m_questTitle = packet.QuestTitle,
            m_questLevel = packet.Level,
            m_mainline = packet.Mainline == 1,
            m_goals = [],
            m_startGoals = [],
        };

        // Store the MobileID for later dialog matching
        s_questNameToMobileIDMap[template.m_questName] = packet.MobileID;

        // Extract the goal compilation from the packet.
        var goalCompilation = ExtractGoalCompilationFromPacket(packet);

        if (goalCompilation is not null) {
            template.m_goals = CraftGoalsFromCompilation(goalCompilation!);

            // All of the goals within the goal compilation are starting goals.
            foreach (var goal in template.m_goals) {
                template.m_startGoals.Add(goal.m_goalName);
            }
        }

        return template;
    }

    private static void AddQuestIDToQuestTemplate(QuestTemplate template, List<SendQuestPacket> packets) {
        // Search through the packets until we find where the quest title matches.
        foreach (var packet in packets) {
            if (packet.QuestTitle == template.m_questTitle) {
                s_questTemplateIDMap[template] = packet.QuestID;

                return;
            }
        }
    }

    private static void AddGoalsToQuestTemplate(QuestTemplate template, List<SendGoalPacket> packets) {
        if (!s_questTemplateIDMap.TryGetValue(template, out var questID)) {
            return;
        }

        // Search through the packets until we find where the quest title matches.
        var counter = template.m_goals.Count;
        foreach (var packet in packets) {
            if (packet.QuestID == questID) {
                // If the template already has this goal, skip.
                if (template.m_goals.Any(g => g.m_goalNameID == packet.GoalNameID)) {
                    continue;
                }

                var placeholderGoalName = $"{++counter}_{packet.GoalTitle}";

                GoalTemplate goalTemplate = GetGoalFromType((GOAL_TYPE) packet.GoalType);
                goalTemplate.m_goalName = placeholderGoalName;
                goalTemplate.m_goalNameID = packet.GoalNameID;
                goalTemplate.m_goalTitle = packet.GoalTitle;
                goalTemplate.m_locationName = packet.GoalLocation;
                goalTemplate.m_destinationZone = packet.GoalDestinationZone;
                goalTemplate.m_displayImage1 = packet.GoalImage1;
                goalTemplate.m_displayImage2 = packet.GoalImage2;
                goalTemplate.m_goalType = (GOAL_TYPE) packet.GoalType;

                if (goalTemplate is BountyGoalTemplate bountyGoalTemplate) {
                    bountyGoalTemplate.m_bountyTotal = (int) packet.GoalTotal;
                }

                // Parse tally counter information from GoalMadlibs if UseTally indicates it's used
                if (packet.UseTally == 1 && packet.GoalMadlibs is not null && packet.GoalMadlibs.Length > 0) {
                    var tallyCounter = ParseTallyCounterFromMadlibs(packet.GoalMadlibs, packet.GoalTotal);
                    if (tallyCounter != null) {
                        goalTemplate.m_tallyCounter = tallyCounter;
                    }
                }
                // If UseTally is 0, explicitly set tally counter to null to disable it in the editor
                else if (packet.UseTally == 0) {
                    goalTemplate.m_tallyCounter = null;
                }

                // Attempt to deserialize the ClientTags field of the packet.
                if (packet.ClientTags is not null && packet.ClientTags.Length > 0) {
                    var clientTags = packet.ClientTags?.Replace(" ", string.Empty);
                    var clientTagsBytes = Convert.FromHexString(clientTags!);
                    var serializer = new ObjectSerializer(false, SerializerFlags.None);
                    try {
                        if (serializer.Deserialize<ClientTagList>(clientTagsBytes, 1, out var clientTagsObject)) {
                            goalTemplate.m_clientTags = clientTagsObject?.m_clientTags;
                        }
                    }
                    catch {
                        // If deserialization fails, we just skip setting the client tags.
                    }
                }

                template.m_goals.Add(goalTemplate);
                
                // Map GoalID to the goal template for dialog processing
                s_goalIDToTemplateMap[packet.GoalID] = goalTemplate;
            }
        }
    }

    private static GoalCompilation? ExtractGoalCompilationFromPacket(QuestOfferPacket packet) {
        try {
            var goalBlob = packet.GoalData.Replace(" ", string.Empty);
            var goalBlobBytes = Convert.FromHexString(goalBlob);
            var serializer = new ObjectSerializer(false, SerializerFlags.None);
            if (!serializer.Deserialize<GoalCompilation>(goalBlobBytes, 1, out var goalCompilation)) {
                return null;
            }

            return goalCompilation;
        }
        catch {
            return null;
        }
    }

    private static List<GoalTemplate> CraftGoalsFromCompilation(GoalCompilation goalCompilation) {
        var goals = new List<GoalTemplate>();

        var counter = 0;
        foreach (var goal in goalCompilation.m_goals) {
            // Create a new instance of the correct type based on the goal type.
            GoalTemplate goalInstance = GetGoalFromType((GOAL_TYPE) goal.m_goalType);

            goalInstance.m_goalName = $"{++counter}_{goal.m_goalTitle}";
            goalInstance.m_goalNameID = goal.m_goalNameID;
            goalInstance.m_goalTitle = goal.m_goalTitle;
            goalInstance.m_locationName = goal.m_goalLocation;
            goalInstance.m_destinationZone = goal.m_goalDestinationZone;
            goalInstance.m_displayImage1 = goal.m_goalImage1;
            goalInstance.m_displayImage2 = goal.m_goalImage2;
            goalInstance.m_goalType = (GOAL_TYPE) goal.m_goalType;

            if (goalInstance is BountyGoalTemplate bountyGoalTemplate) {
                bountyGoalTemplate.m_bountyTotal = goal.m_goalTotal;
            }

            // Create a basic tally counter for goals from quest compilation
            // These won't have UseTally or madlibs data, so we create enabled defaults
            goalInstance.m_tallyCounter = new TallyCounterTemplate {
                m_percentChance = 1.0f,                                     // Default to 100% chance
                m_count = goal.m_goalTotal,                                 // Use goal total as tally count  
                m_descriptor = "",                                          // Empty - will need manual setup
                m_descriptor2 = "",                                         // Empty - will need manual setup
                m_tallyResults = new ResultList { m_results = new List<Result>() } // Empty results list
            };

            goals.Add(goalInstance);
        }

        return goals;
    }

    private static void AddPrepDialogToQuestTemplate(QuestTemplate template, List<ActorDialogPacket> packets) {
        // Get the MobileID for this quest template
        if (!s_questNameToMobileIDMap.TryGetValue(template.m_questName, out var questMobileID)) {
            return; // No MobileID found for this quest template
        }

        // Find the ActorDialog packet with matching MobileID and CompletionType "QuestInfo"
        foreach (var packet in packets) {
            if (packet.MobileID == questMobileID && 
                packet.CompletionType?.Equals("QuestInfo", StringComparison.OrdinalIgnoreCase) == true) {
                try {
                    // Deserialize the ActorDialog hex blob.
                    var actorDialogBlob = packet.ActorDialog.Replace(" ", string.Empty);
                    var actorDialogBytes = Convert.FromHexString(actorDialogBlob);
                    var serializer = new ObjectSerializer(
                        Versionable: false,
                        Behaviors: SerializerFlags.None
                    );

                    if (serializer.Deserialize<ActorDialog>(actorDialogBytes, 16, out var actorDialog)) {
                        // Initialize dialog list if it doesn't exist
                        if (template.m_dialogList == null) {
                            template.m_dialogList = new ActorDialogList { m_dialogs = [] };
                        }

                        // Cast to ActorDialogList to access m_dialogs property
                        var dialogList = template.m_dialogList as ActorDialogList;
                        if (dialogList == null) {
                            // If it's not an ActorDialogList, create a new one
                            dialogList = new ActorDialogList { m_dialogs = [] };
                            template.m_dialogList = dialogList;
                        }

                        // Set the dialog tag to "Prep" for quest preparation dialogue
                        actorDialog!.m_dialogTag = "Prep";

                        // Populate actor template IDs using persona name from the packet
                        PopulateActorTemplateIds(actorDialog, packet.Persona);

                        // Add or update the prep dialog
                        var existingPrepDialog = dialogList.m_dialogs?.FirstOrDefault(d =>
                            d.m_dialogTag.ToString().Equals("Prep", StringComparison.OrdinalIgnoreCase) == true);

                        if (existingPrepDialog != null) {
                            // Update existing prep dialog
                            var index = dialogList.m_dialogs!.IndexOf(existingPrepDialog);
                            dialogList.m_dialogs[index] = actorDialog;
                        }
                        else {
                            // Add new prep dialog
                            dialogList.m_dialogs?.Add(actorDialog);
                        }

                        // We found and processed the prep dialog for this quest, no need to continue
                        break;
                    }
                }
                catch {
                    // If deserialization fails, we skip this dialog packet
                    // This allows the system to continue processing other quests
                }
            }
        }
    }

    private static void AddCompletionDialogToQuestTemplate(QuestTemplate template, List<ActorDialogPacket> packets) {
        // Find the quest ID by searching for a template with the same quest name
        var questIDEntry = s_questTemplateIDMap.FirstOrDefault(kvp =>
            kvp.Key.m_questName.ToString().Equals(template.m_questName, StringComparison.OrdinalIgnoreCase) == true);

        if (questIDEntry.Key == null) {
            return; // No matching quest found in the map
        }

        var questID = questIDEntry.Value;

        foreach (var packet in packets) {
            if (packet.CompletionType?.Equals("Completion", StringComparison.OrdinalIgnoreCase) == true &&
                packet.QuestID == questID) {
                try {
                    // Deserialize the ActorDialog hex blob using the same serializer configuration
                    var actorDialogBlob = packet.ActorDialog.Replace(" ", string.Empty);
                    var actorDialogBytes = Convert.FromHexString(actorDialogBlob);
                    var serializer = new ObjectSerializer(
                        Versionable: false,
                        Behaviors: SerializerFlags.None
                    );

                    if (serializer.Deserialize<ActorDialog>(actorDialogBytes, 16, out var actorDialog)) {
                        // Initialize dialog list if it doesn't exist
                        if (template.m_dialogList == null) {
                            template.m_dialogList = new ActorDialogList { m_dialogs = [] };
                        }

                        // Cast to ActorDialogList to access m_dialogs property
                        var dialogList = template.m_dialogList as ActorDialogList;
                        if (dialogList == null) {
                            // If it's not an ActorDialogList, create a new one
                            dialogList = new ActorDialogList { m_dialogs = [] };
                            template.m_dialogList = dialogList;
                        }

                        // Set the dialog tag to "Completion" for quest completion dialogue
                        actorDialog!.m_dialogTag = "Completion";

                        // Populate actor template IDs using persona name from the packet
                        PopulateActorTemplateIds(actorDialog, packet.Persona);

                        // Add or update the completion dialog
                        var existingCompletionDialog = dialogList.m_dialogs?.FirstOrDefault(d =>
                            d.m_dialogTag.ToString().Equals("Completion", StringComparison.OrdinalIgnoreCase) == true);

                        if (existingCompletionDialog != null) {
                            // Update existing completion dialog
                            var index = dialogList.m_dialogs!.IndexOf(existingCompletionDialog);
                            dialogList.m_dialogs[index] = actorDialog;
                        }
                        else {
                            // Add new completion dialog
                            dialogList.m_dialogs?.Add(actorDialog);
                        }

                        // Found and processed the completion dialog for this specific quest
                        break;
                    }
                }
                catch {
                    // If deserialization fails, we skip this dialog packet
                    // This allows the system to continue processing other completion dialogs
                }
            }
        }
    }

    private static void AddDialogToGoals(List<ActorDialogPacket> packets) {
        // Process MSG_ACTORDIALOG packets that have GoalID != 0 for goal-specific dialogs
        foreach (var packet in packets) {
            if (packet.GoalID != 0 && s_goalIDToTemplateMap.TryGetValue(packet.GoalID, out var goalTemplate)) {
                try {
                    // Deserialize the ActorDialog hex blob using the same serializer configuration
                    var actorDialogBlob = packet.ActorDialog.Replace(" ", string.Empty);
                    var actorDialogBytes = Convert.FromHexString(actorDialogBlob);
                    var serializer = new ObjectSerializer(
                        Versionable: false,
                        Behaviors: SerializerFlags.None
                    );

                    if (serializer.Deserialize<ActorDialog>(actorDialogBytes, 16, out var actorDialog)) {
                        // Initialize dialog list if it doesn't exist
                        if (goalTemplate.m_dialogList == null) {
                            goalTemplate.m_dialogList = new ActorDialogList { m_dialogs = [] };
                        }

                        // Cast to ActorDialogList to access m_dialogs property
                        var dialogList = goalTemplate.m_dialogList as ActorDialogList;
                        if (dialogList == null) {
                            // If it's not an ActorDialogList, create a new one
                            dialogList = new ActorDialogList { m_dialogs = [] };
                            goalTemplate.m_dialogList = dialogList;
                        }

                        // Map CompletionType to appropriate dialog tag
                        var dialogTag = packet.CompletionType?.ToLower() switch {
                            "questinfo" => "QuestInfo",
                            "prep" => "Prep", 
                            "underway" => "Underway",
                            "completion" => "Completion",
                            "hyperlink" => "Hyperlink",
                            _ => packet.CompletionType ?? "QuestInfo" // Default fallback
                        };

                        actorDialog!.m_dialogTag = dialogTag;

                        // Populate actor template IDs using persona name from the packet
                        PopulateActorTemplateIds(actorDialog, packet.Persona);

                        // Add or update the dialog for this tag
                        var existingDialog = dialogList.m_dialogs?.FirstOrDefault(d =>
                            d.m_dialogTag.ToString().Equals(dialogTag, StringComparison.OrdinalIgnoreCase) == true);

                        if (existingDialog != null) {
                            // Update existing dialog
                            var index = dialogList.m_dialogs!.IndexOf(existingDialog);
                            dialogList.m_dialogs[index] = actorDialog;
                        }
                        else {
                            // Add new dialog
                            dialogList.m_dialogs?.Add(actorDialog);
                        }
                    }
                }
                catch {
                    // If deserialization fails, we skip this dialog packet
                    // This allows the system to continue processing other goal dialogs
                }
            }
        }
    }

    /// <summary>
    /// Tries to load the template manifest for actor template ID resolution.
    /// This method attempts to load via delegate if provided, otherwise skips gracefully.
    /// </summary>
    private static void TryLoadTemplateManifest() {
        try {
            // Try to use the external template manifest loader if available
            if (TemplateManifestLoader != null) {
                var manifestData = TemplateManifestLoader();
                if (manifestData.HasValue) {
                    TemplateManifestService.Instance.LoadFromFileData(manifestData.Value);
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Warning: Could not load template manifest: {ex.Message}");
        }
    }

    /// <summary>
    /// Optional delegate to provide template manifest data from external sources (like RootWadService).
    /// Set this delegate to enable actor template ID resolution.
    /// </summary>
    public static Func<Memory<byte>?>? TemplateManifestLoader { get; set; }

    /// <summary>
    /// Populates actor template IDs for all dialog entries in an ActorDialog using the persona name from the packet.
    /// </summary>
    /// <param name="actorDialog">The ActorDialog containing dialog entries to populate.</param>
    /// <param name="personaName">The persona name from the packet (e.g., "WC-ST01-NPC04_Persona").</param>
    private static void PopulateActorTemplateIds(ActorDialog actorDialog, string personaName) {
        if (actorDialog?.m_dialogEntries == null || string.IsNullOrEmpty(personaName)) {
            return;
        }

        // Get the template ID from the persona name
        var templateId = TemplateManifestService.Instance.GetTemplateIdByPersonaName(personaName);
        if (templateId == 0) {
            // If we can't find the template ID, log it but continue processing
            Console.WriteLine($"Warning: Could not find template ID for persona: {personaName}");
            return;
        }

        // Set the actor template ID for all dialog entries
        foreach (var entry in actorDialog.m_dialogEntries) {
            if (entry != null) {
                entry.m_actorTemplateID = templateId;
            }
        }
    }

    private static GoalTemplate GetGoalFromType(GOAL_TYPE goalType)
        => goalType switch {
            GOAL_TYPE.GOAL_TYPE_BOUNTY => new BountyGoalTemplate(),
            GOAL_TYPE.GOAL_TYPE_BOUNTYCOLLECT => new BountyGoalTemplate(),
            GOAL_TYPE.GOAL_TYPE_SCAVENGE => new ScavengeGoalTemplate(),
            GOAL_TYPE.GOAL_TYPE_USAGE => new ScavengeGoalTemplate(),
            GOAL_TYPE.GOAL_TYPE_PERSONA => new PersonaGoalTemplate(),
            GOAL_TYPE.GOAL_TYPE_WAYPOINT => new WaypointGoalTemplate(),
            GOAL_TYPE.GOAL_TYPE_ACHIEVERANK => new AchieveRankGoalTemplate(),
            _ => throw new NotSupportedException($"Unsupported goal type: {goalType}"),
        };

    /// <summary>
    /// Parses tally counter information from the GoalMadlibs hex string.
    /// Extracts TALLYTEXT and TALLYTEXT2 locale references from the MadlibBlock.
    /// </summary>
    /// <param name="goalMadlibs">Hex string containing serialized MadlibBlock data</param>
    /// <param name="goalTotal">The goal total to use as tally count</param>
    /// <returns>Configured TallyCounterTemplate or null if parsing fails</returns>
    private static TallyCounterTemplate? ParseTallyCounterFromMadlibs(string goalMadlibs, uint goalTotal) {
        try {
            var madlibsBlob = goalMadlibs.Replace(" ", string.Empty);
            var madlibsBytes = Convert.FromHexString(madlibsBlob);
            var serializer = new ObjectSerializer(false, SerializerFlags.None);
            
            if (!serializer.Deserialize<MadlibBlock>(madlibsBytes, 1, out var madlibBlock)) {
                return null;
            }

            // Look for TALLYTEXT and TALLYTEXT2 tokens in the madlibs
            string? descriptor = null;
            string? descriptor2 = null;

            foreach (var madlib in madlibBlock?.m_madlibs ?? []) {
                if (madlib is MadlibArgT_ByteString stringMadlib) {
                    switch (stringMadlib.m_madlibToken.ToString().ToUpperInvariant()) {
                        case "TALLYTEXT":
                            descriptor = stringMadlib.m_madlibArgument;
                            break;
                        case "TALLYTEXT2":
                            descriptor2 = stringMadlib.m_madlibArgument;
                            break;
                    }
                }
            }

            // Create tally counter template with extracted data
            return new TallyCounterTemplate {
                m_percentChance = 1.0f,                                     // Default to 100% chance
                m_count = (int)goalTotal,                                   // Use GoalTotal as tally count
                m_descriptor = descriptor ?? "",                            // TALLYTEXT locale reference
                m_descriptor2 = descriptor2 ?? "",                          // TALLYTEXT2 locale reference
                m_tallyResults = new ResultList { m_results = new List<Result>() } // Empty results list
            };
        }
        catch {
            // If deserialization fails, return null to indicate no tally counter available
            return null;
        }
    }

}