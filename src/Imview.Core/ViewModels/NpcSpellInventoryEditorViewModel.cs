// Copyright (c) 2025 Jay Kulsh
// Licensed under the BSD 3-Clause License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Imview.Core.Database.Models;
using Imview.Core.Database.Services;
using ReactiveUI;

namespace Imview.Core.ViewModels
{
    /// <summary>
    /// ViewModel for editing NPC spell inventories
    /// </summary>
    public class NpcSpellInventoryEditorViewModel : ViewModelBase
    {
        private ulong _npcTemplateId;
        private string _npcName = string.Empty;
        private NpcSpellInventory? _originalSpellInventory;
        private bool _wasSaved = false;
        private bool _isDirty = false;

        public NpcSpellInventoryEditorViewModel(ulong npcTemplateId, string npcName, NpcSpellInventory? existingSpellInventory = null)
        {
            _npcTemplateId = npcTemplateId;
            _npcName = npcName;
            _originalSpellInventory = existingSpellInventory;
            
            Spells = new ObservableCollection<SpellInventoryItemViewModel>();
            
            // Load existing spells if available
            if (existingSpellInventory?.Spells != null)
            {
                foreach (var spell in existingSpellInventory.Spells)
                {
                    var spellVm = new SpellInventoryItemViewModel(spell);
                    spellVm.PropertyChanged += OnSpellPropertyChanged;
                    Spells.Add(spellVm);
                }
            }
            
            // Commands
            AddSpellCommand = ReactiveCommand.Create(AddSpell);
            RemoveSpellCommand = ReactiveCommand.Create<SpellInventoryItemViewModel>(RemoveSpell);
            SaveCommand = ReactiveCommand.CreateFromTask(Save);
            CancelCommand = ReactiveCommand.Create(() => { });
        }

        public ulong NpcTemplateId => _npcTemplateId;
        public string NpcName => _npcName;
        public string WindowTitle => $"Edit Spell Inventory - {_npcName} (ID: {_npcTemplateId})";
        
        public ObservableCollection<SpellInventoryItemViewModel> Spells { get; }
        
        public bool WasSaved => _wasSaved;
        
        public bool IsDirty
        {
            get => _isDirty;
            private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
        }

        public ReactiveCommand<Unit, Unit> AddSpellCommand { get; }
        public ReactiveCommand<SpellInventoryItemViewModel, Unit> RemoveSpellCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        private void AddSpell()
        {
            var newSpell = new SpellInventoryItemViewModel();
            newSpell.PropertyChanged += OnSpellPropertyChanged;
            Spells.Add(newSpell);
            IsDirty = true;
        }

        private void RemoveSpell(SpellInventoryItemViewModel spell)
        {
            if (spell != null)
            {
                spell.PropertyChanged -= OnSpellPropertyChanged;
                Spells.Remove(spell);
                IsDirty = true;
            }
        }

        private void OnSpellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            IsDirty = true;
        }

        private async Task Save()
        {
            try
            {
                // Validate all spells
                var validationErrors = ValidateSpells();
                if (validationErrors.Count > 0)
                {
                    var errorMessage = string.Join("\n", validationErrors);
                    throw new ValidationException($"Validation failed:\n{errorMessage}");
                }

                // Create spell inventory to save
                var spellInventory = new NpcSpellInventory(_npcTemplateId);
                
                foreach (var spellVm in Spells)
                {
                    spellInventory.Spells.Add(new SpellInventoryItem(
                        spellVm.TemplateID,
                        spellVm.RequiredSpellID,
                        spellVm.Level
                    ));
                }

                // Save to database
                var success = await NpcSpellInventoryService.SaveNpcSpellInventoryAsync(spellInventory);
                if (success)
                {
                    _wasSaved = true;
                    IsDirty = false;
                    Console.WriteLine($"[INFO] Successfully saved spell inventory for NPC {_npcName} (ID: {_npcTemplateId})");
                }
                else
                {
                    throw new Exception("Failed to save spell inventory to data pack");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to save NPC spell inventory: {ex.Message}");
                throw; // Re-throw to let the UI handle the error display
            }
        }

        private System.Collections.Generic.List<string> ValidateSpells()
        {
            var errors = new System.Collections.Generic.List<string>();
            
            // Check for duplicate spell template IDs
            var duplicateSpells = Spells
                .Where(s => s.TemplateID > 0)
                .GroupBy(s => s.TemplateID)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
                
            if (duplicateSpells.Count > 0)
            {
                errors.Add($"Duplicate spell template IDs found: {string.Join(", ", duplicateSpells)}");
            }
            
            // Validate each spell individually
            for (int i = 0; i < Spells.Count; i++)
            {
                var spell = Spells[i];
                var spellErrors = spell.Validate();
                if (spellErrors.Count > 0)
                {
                    errors.Add($"Spell #{i + 1}: {string.Join(", ", spellErrors)}");
                }
            }
            
            return errors;
        }
    }
    
    /// <summary>
    /// ViewModel for individual spell inventory items
    /// </summary>
    public class SpellInventoryItemViewModel : ViewModelBase
    {
        private ulong _templateID;
        private ulong _requiredSpellID;
        private int _level = 0;

        public SpellInventoryItemViewModel(SpellInventoryItem? spell = null)
        {
            if (spell != null)
            {
                _templateID = spell.TemplateID;
                _requiredSpellID = spell.RequiredSpellID;
                _level = spell.Level;
            }
        }

        public ulong TemplateID
        {
            get => _templateID;
            set => this.RaiseAndSetIfChanged(ref _templateID, value);
        }

        public ulong RequiredSpellID
        {
            get => _requiredSpellID;
            set => this.RaiseAndSetIfChanged(ref _requiredSpellID, value);
        }

        public int Level
        {
            get => _level;
            set => this.RaiseAndSetIfChanged(ref _level, value);
        }
        
        public string RequiredSpellText => RequiredSpellID == 0 ? "None" : RequiredSpellID.ToString();

        public System.Collections.Generic.List<string> Validate()
        {
            var errors = new System.Collections.Generic.List<string>();
            
            if (TemplateID == 0)
            {
                errors.Add("Spell Template ID is required");
            }
            
            // Removed level validation - any level value is allowed
            
            return errors;
        }
    }
}
