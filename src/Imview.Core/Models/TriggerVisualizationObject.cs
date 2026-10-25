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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.ViewModels;
using Imview.Core.Common.Extensions;

namespace Imview.Core.Models;

public class TriggerVisualizationObject
{
    public string Name { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public uint TriggerMax { get; set; }
    public uint Cooldown { get; set; }
    public uint CooldownRand { get; set; }
    public bool IsPulsar { get; set; }
    public List<string> ActivateEvents { get; set; } = new();
    public List<string> FireEvents { get; set; } = new();
    public List<string> DeactivateEvents { get; set; } = new();
    public List<string> UnknownEvents { get; set; } = new();
    public string UnknownString { get; set; } = string.Empty;
    public uint UnknownUint { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public Trigger OriginalTrigger { get; set; } = null!;
    public List<ObjectFlag> Flags { get; set; } = new();

    /// <summary>
    /// Wrapped results for JSON display
    /// </summary>
    public ObservableCollection<ResultWrapper> WrappedResults { get; } = new();

    /// <summary>
    /// Wrapped requirements for JSON display
    /// </summary>
    public ObservableCollection<RequirementWrapper> WrappedRequirements { get; } = new();
    
    /// <summary>
    /// Gets all events combined for UI display
    /// </summary>
    public List<string> Events
    {
        get
        {
            var allEvents = new List<string>();
            if (ActivateEvents.Any())
                allEvents.AddRange(ActivateEvents.Select(e => $"Activate: {e}"));
            if (FireEvents.Any())
                allEvents.AddRange(FireEvents.Select(e => $"Fire: {e}"));
            if (DeactivateEvents.Any())
                allEvents.AddRange(DeactivateEvents.Select(e => $"Deactivate: {e}"));
            if (UnknownEvents.Any())
                allEvents.AddRange(UnknownEvents.Select(e => $"Unknown: {e}"));
            return allEvents;
        }
    }

    public TriggerVisualizationObject(Trigger trigger, string? zoneName = null)
    {
        OriginalTrigger = trigger;
#pragma warning disable CS8601 // Possible null reference assignment
        Name = trigger.m_triggerName.OrIfEmpty("Unnamed Trigger");
#pragma warning restore CS8601

        // Ensure Name is never null or empty
        if (string.IsNullOrEmpty(Name))
        {
            Name = "Unnamed Trigger";
        }

        TriggerMax = trigger.m_triggerMax;
        Cooldown = trigger.m_cooldown;
        CooldownRand = trigger.m_cooldownRand;
        IsPulsar = trigger.m_pulsar;
        UnknownUint = trigger.unknown_uint_3;

#pragma warning disable CS8601 // Possible null reference assignment
        UnknownString = trigger.unknown_str_3.ToString();
#pragma warning restore CS8601

        // Determine trigger type - this is a simplified classification
        TriggerType = IsPulsar ? "Pulsar" : "Standard";

        // Set the zone name from parameter
        Zone = zoneName ?? string.Empty;
        
        // Extract location from trigger object info if available
        if (trigger.m_triggerObjInfo != null)
        {
            X = trigger.m_triggerObjInfo.m_locationX;
            Y = trigger.m_triggerObjInfo.m_locationY;
            Z = trigger.m_triggerObjInfo.m_locationZ;
        }
        
        // Convert ByteString lists to string lists
        if (trigger.m_activateEvents != null)
        {
            ActivateEvents = trigger.m_activateEvents
                .Where(e => !e.IsEmpty)
                .Select(e => e.ToString() ?? string.Empty)
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
        }
        
        if (trigger.m_fireEvents != null)
        {
            FireEvents = trigger.m_fireEvents
                .Where(e => !e.IsEmpty)
                .Select(e => e.ToString() ?? string.Empty)
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
        }
        
        if (trigger.m_deactivateEvents != null)
        {
            DeactivateEvents = trigger.m_deactivateEvents
                .Where(e => !e.IsEmpty)
                .Select(e => e.ToString() ?? string.Empty)
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
        }
        
        if (trigger.m_unknown != null)
        {
            UnknownEvents = trigger.m_unknown
                .Where(e => !e.IsEmpty)
                .Select(e => e.ToString() ?? string.Empty)
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
        }

        // Wrap results for JSON display
        if (trigger.m_results?.m_results != null)
        {
            foreach (var result in trigger.m_results.m_results)
            {
                if (result != null)
                {
                    WrappedResults.Add(new ResultWrapper(result, Zone, Name));
                }
            }
        }

        // Wrap requirements for JSON display
        if (trigger.m_requirements?.m_requirements != null)
        {
            foreach (var requirement in trigger.m_requirements.m_requirements)
            {
                if (requirement != null)
                {
                    WrappedRequirements.Add(new RequirementWrapper(requirement));
                }
            }
        }
    }
    
    /// <summary>
    /// Gets a formatted string describing the trigger's events
    /// </summary>
    public string EventsText
    {
        get
        {
            var events = new List<string>();
            if (ActivateEvents.Any())
                events.Add($"Activate: {string.Join(", ", ActivateEvents)}");
            if (FireEvents.Any())
                events.Add($"Fire: {string.Join(", ", FireEvents)}");
            if (DeactivateEvents.Any())
                events.Add($"Deactivate: {string.Join(", ", DeactivateEvents)}");
            if (UnknownEvents.Any())
                events.Add($"Unknown: {string.Join(", ", UnknownEvents)}");
            return events.Any() ? string.Join(" | ", events) : "No events";
        }
    }
    
    /// <summary>
    /// Gets a formatted string describing the trigger's timing properties
    /// </summary>
    public string TimingText
    {
        get
        {
            var timing = new List<string>();
            if (TriggerMax > 0)
                timing.Add($"Max: {TriggerMax}");
            if (Cooldown > 0)
                timing.Add($"Cooldown: {Cooldown}s");
            if (CooldownRand > 0)
                timing.Add($"±{CooldownRand}s");
            if (IsPulsar)
                timing.Add("Pulsar");
            return timing.Any() ? string.Join(", ", timing) : "No timing constraints";
        }
    }
    
    /// <summary>
    /// Gets a formatted string describing the trigger's location
    /// </summary>
    public string LocationText
    {
        get
        {
            return $"({X:F1}, {Y:F1}, {Z:F1})";
        }
    }
}
