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
using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Common.Constants;
using System.Linq;
using Imcodec.IO;
using Imview.Core.Common.Extensions;

namespace Imview.Core.Controls.Goals;

public class ScavengeGoalEditor : GoalEditorWindowBase {

    private readonly TextBox _itemAdjectivesBox;
    private readonly TextBox _itemTotalBox;
    private ScavengeGoalTemplate ScavengeTemplate => (ScavengeGoalTemplate)Template;

    public ScavengeGoalEditor(ScavengeGoalTemplate? template = null)
        : base(template ?? new ScavengeGoalTemplate(), "Edit Scavenge Goal") {
        _itemAdjectivesBox = new TextBox();
        _itemTotalBox = new TextBox();

        InitializeEditor();
    }

    public void InitializeEditor() {
        var mainPanel = CreateBaseControlsPanel();

        var scavengeContent = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                CreateLabeledControl("Item Adjectives (comma-separated):", _itemAdjectivesBox),
                CreateLabeledControl("Item Total:", _itemTotalBox)
            }
        };

        mainPanel.Children.Add(CreateGroupBox("Scavenge Settings", scavengeContent));
        mainPanel.Children.Add(CreateActionButtons(Save, Cancel));

        if (ScavengeTemplate != null) {
            _itemAdjectivesBox.Text = string.Join(", ", ScavengeTemplate.m_itemAdjectives ?? []);
            _itemTotalBox.Text = ScavengeTemplate.m_itemTotal.ToString();
        }
    }

    public Control GetTypeSpecificControls() 
        => new StackPanel {
            Children = {
                _itemAdjectivesBox,
                 _itemTotalBox
            }
        };

    public bool ValidateState() 
        => !string.IsNullOrWhiteSpace(_itemTotalBox.Text) &&
            int.TryParse(_itemTotalBox.Text, out _);

    public void SaveTypeSpecificValues() {
        var adjectives = (_itemAdjectivesBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => new ByteString(a.Trim()))
            .ToList();

        if (int.TryParse(_itemTotalBox.Text, out int itemTotal)) {
            ScavengeTemplate.m_itemAdjectives = adjectives.Select(a => a.ToString()).ToByteStrings();
            ScavengeTemplate.m_itemTotal = itemTotal;
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