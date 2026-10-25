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
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Common.Extensions;

namespace Imview.Core.Models;

public class VolumeVisualizationObject
{
    public string Name { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public ulong TemplateID { get; set; }
    public string PrimitiveType { get; set; } = "Unknown";
    public float Radius { get; set; }
    public float Length { get; set; }
    public float Width { get; set; }
    public List<string> EnterEvents { get; set; } = new();
    public List<string> ExitEvents { get; set; } = new();
    public Volume OriginalVolume { get; set; } = null!;

    public VolumeVisualizationObject(Volume volume)
    {
        OriginalVolume = volume;
#pragma warning disable CS8601 // Possible null reference assignment
        Name = volume.m_volumeName.OrIfEmpty($"Volume_{volume.m_templateID}");
        X = volume.m_locationX;
        Y = volume.m_locationY;
        Z = volume.m_locationZ;
        TemplateID = volume.m_templateID;
        PrimitiveType = volume.m_primitiveType.OrIfEmpty("Unknown");
        // Ensure PrimitiveType is never null or empty
        if (string.IsNullOrEmpty(PrimitiveType))
        {
            PrimitiveType = "Unknown";
        }
#pragma warning restore CS8601
        Radius = volume.m_radius;
        Length = volume.m_length;
        Width = volume.m_width;
        
        // Convert ByteString lists to string lists
        if (volume.m_enterEvents != null)
        {
            EnterEvents = volume.m_enterEvents
                .Where(e => !e.IsEmpty)
                .Select(e => e.ToString() ?? string.Empty)
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
        }
        
        if (volume.m_exitEvents != null)
        {
            ExitEvents = volume.m_exitEvents
                .Where(e => !e.IsEmpty)
                .Select(e => e.ToString() ?? string.Empty)
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
        }
    }
    
    /// <summary>
    /// Gets a formatted string describing the volume's events
    /// </summary>
    public string EventsText
    {
        get
        {
            var events = new List<string>();
            if (EnterEvents.Any())
                events.Add($"Enter: {string.Join(", ", EnterEvents)}");
            if (ExitEvents.Any())
                events.Add($"Exit: {string.Join(", ", ExitEvents)}");
            return events.Any() ? string.Join(" | ", events) : "No events";
        }
    }
    
    /// <summary>
    /// Gets a formatted string describing the volume's dimensions
    /// </summary>
    public string DimensionsText
    {
        get
        {
            var primitiveType = PrimitiveType?.ToLowerInvariant() ?? "unknown";
            return primitiveType switch
            {
                "sphere" => $"R: {Radius:F1}",
                "box" => $"L: {Length:F1}, W: {Width:F1}",
                "cylinder" => $"R: {Radius:F1}, L: {Length:F1}",
                _ => $"R: {Radius:F1}, L: {Length:F1}, W: {Width:F1}"
            };
        }
    }
}
