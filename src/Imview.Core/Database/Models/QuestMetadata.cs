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
using System.Text.Json.Serialization;

namespace Imview.Core.Database.Models;

/// <summary>
/// Represents quest metadata stored separately from the QuestTemplate for editor purposes
/// </summary>
public class QuestMetadata {
    // Files from the old database carry an "Id". Declaring it ignored keeps it out of the unmodelled
    // properties a save writes back (SpiralJsonOptions), so the next save of such a file drops it.
    [JsonIgnore]
    public string? Id { get; set; }
    public string QuestTemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = Environment.UserName;
    public string ModifiedBy { get; set; } = Environment.UserName;
}