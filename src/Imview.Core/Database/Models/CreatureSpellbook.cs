// Copyright (c) 2025 Jay Kulsh
// Licensed under the BSD 3-Clause License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Imview.Core.Database.Models
{
    /// <summary>
    /// A creature spellbook entry in the data pack.
    /// Stored in the CreatureSpellbook folder.
    /// </summary>
    public class CreatureSpellbook
    {
        /// <summary>
        /// The deck name (e.g., value of DeckBehaviorTemplate.m_defaultDeck)
        /// </summary>
        [Required]
        public string DeckName { get; set; } = string.Empty;

        /// <summary>
        /// Spell template IDs stored as signed 64-bit to ensure JSON serializer compatibility
        /// </summary>
        [Required]
        public List<long> SpellTemplateIds { get; set; } = new List<long>();
    }
}
