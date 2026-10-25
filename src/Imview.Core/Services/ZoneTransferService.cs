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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Imview.Core.Data;
using Imview.Core.Models;
using Imcodec.ObjectProperty.TypeCache;

namespace Imview.Core.Services;

/// <summary>
/// Zone transfers (the teleports behind a zone's triggers) in the data pack's <c>ZoneTransfer</c> folder, keyed by zone name.
/// Reads return copies: an edit reaches the data pack only through <see cref="SaveZoneTransferAsync"/>.
/// </summary>
public static class ZoneTransferService {

    public static async Task<ResTeleport?> GetTeleportDataAsync(string zoneName, string triggerName) {
        var zone = await WorldStoreProvider.Current.GetAsync<WizardZoneData>(zoneName);
        var teleport = zone?.Teleports.FirstOrDefault(t => t.TriggerName == triggerName)?.Teleport;
        return teleport is null ? null : WorldRecordCopy.Detached(teleport);
    }

    public static async Task<bool> HasTeleportDataAsync(string zoneName, string triggerName) {
        try {
            return await GetTeleportDataAsync(zoneName, triggerName) != null;
        } catch (Exception ex) {
            Console.WriteLine($"Error checking teleport data existence: {ex.Message}");
            return false;
        }
    }

    public static async Task<WizardZoneData[]> GetAllZoneTransfersAsync() {
        var zones = await WorldStoreProvider.Current.GetAllAsync<WizardZoneData>();
        return zones.Select(WorldRecordCopy.Detached).ToArray();
    }

    public static async Task<WizardZoneData?> GetZoneTransferAsync(string zoneName) {
        var zone = await WorldStoreProvider.Current.GetAsync<WizardZoneData>(zoneName);
        return zone is null ? null : WorldRecordCopy.Detached(zone);
    }

    /// <summary>
    /// Writes the zone whole: over the file it was loaded from, or to a new file when no zone has its name.
    /// </summary>
    public static Task SaveZoneTransferAsync(WizardZoneData zone)
        => WorldStoreProvider.Current.SaveAsync(WorldRecordCopy.Detached(zone));

    public static Task DeleteZoneTransferAsync(string zoneName)
        => WorldStoreProvider.Current.DeleteAsync<WizardZoneData>(zoneName);

    /// <summary>
    /// The <c>m_destinationLoc</c> text for coordinates (x, y, z and optionally yaw): comma-separated, '.' as decimal separator in every culture.
    /// </summary>
    public static string FormatDestinationLoc(params float[] coordinates)
        => string.Join(",", coordinates.Select(c => c.ToString(CultureInfo.InvariantCulture)));
}
