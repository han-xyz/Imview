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

using Avalonia.Controls;
using Imview.Core.Controls.Base;
using Imcodec.ObjectProperty.TypeCache;
using System;
using Imview.Core.Common.Constants;
using System.Collections.Generic;
using System.Linq;
using Imview.Core.Common.Extensions;

namespace Imview.Core.Controls.Goals;

public class BountyGoalEditor : GoalEditorWindowBase {

    private readonly TextBox _adjectivesBox;
    private readonly TextBox _bountyTotalBox;
    private readonly ComboBox _bountyTypeBox;
    private BountyGoalTemplate BountyTemplate => (BountyGoalTemplate) Template;

    public BountyGoalEditor(BountyGoalTemplate? template = null)
        : base(template ?? new BountyGoalTemplate(), "Edit Bounty Goal") {
        _adjectivesBox = new TextBox();
        _bountyTotalBox = new TextBox();
        _bountyTypeBox = new ComboBox {
            ItemsSource = Enum.GetValues<BOUNTY_TYPE>()
        };

        InitializeEditor();
    }

    public void InitializeEditor() {
        var mainPanel = CreateBaseControlsPanel();

        var bountyContent = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                CreateLabeledControl("Adjectives (comma-separated):", _adjectivesBox),
                CreateLabeledControl("Bounty Total:", _bountyTotalBox),
                CreateLabeledControl("Bounty Type:", _bountyTypeBox)
            }
        };

        mainPanel.Children.Add(CreateGroupBox("Bounty Settings", bountyContent));
        mainPanel.Children.Add(CreateActionButtons(Save, Cancel));

        if (BountyTemplate != null) {
            _adjectivesBox.Text = string.Join(", ", BountyTemplate.m_npcAdjectives ?? []);
            _bountyTotalBox.Text = BountyTemplate.m_bountyTotal.ToString();
            _bountyTypeBox.SelectedItem = BountyTemplate.m_bountyType;
        }
    }

    public Control GetTypeSpecificControls()
        => new StackPanel {
            Children = {
                    _adjectivesBox,
                    _bountyTotalBox,
                    _bountyTypeBox
                }
        };

    public bool ValidateState()
        => !string.IsNullOrWhiteSpace(_bountyTotalBox.Text) &&
            int.TryParse(_bountyTotalBox.Text, out _) &&
            _bountyTypeBox.SelectedItem != null;

    public void SaveTypeSpecificValues() {
        var adjectives = (_adjectivesBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .ToList();

        if (int.TryParse(_bountyTotalBox.Text, out int bountyTotal)) {
            BountyTemplate.m_npcAdjectives = adjectives.ToByteStrings();
            BountyTemplate.m_bountyTotal = bountyTotal;
            BountyTemplate.m_bountyType = (BOUNTY_TYPE) _bountyTypeBox.SelectedItem!;
            // Note: m_goalType is set by the base class SaveValues() from the ComboBox selection
        }
    }
    
    protected override void Save() {
        if (!ValidateState()) {
            // TODO: Show validation error
            return;
        }

        SaveValues();
        SaveTypeSpecificValues();
        ResultSource.SetResult(Template);
        Close();
    }

}