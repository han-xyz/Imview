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
using Imcodec.IO;
using Imview.Core.Common.Constants;
using Imview.Core.Controls.Base;
using Imcodec.ObjectProperty.TypeCache;

namespace Imview.Core.Controls.Goals;

public class WaypointGoalEditor : GoalEditorWindowBase {

    private readonly CheckBox _zoneEntryBox;
    private readonly CheckBox _zoneExitBox;
    private readonly TextBox _zoneTagBox;
    private readonly TextBox _proximityTagBox;
    private WaypointGoalTemplate WaypointTemplate => (WaypointGoalTemplate) Template;

    public WaypointGoalEditor(WaypointGoalTemplate? template = null)
        : base(template ?? new WaypointGoalTemplate(), "Edit Waypoint Goal") {
        _zoneEntryBox = new CheckBox { Content = "Zone Entry" };
        _zoneExitBox = new CheckBox { Content = "Zone Exit" };
        _zoneTagBox = new TextBox();
        _proximityTagBox = new TextBox();

        InitializeEditor();
    }

    public void InitializeEditor() {
        var mainPanel = CreateBaseControlsPanel();

        var waypointContent = new StackPanel {
            Spacing = EditorConstants.DEFAULT_CONTROL_SPACING,
            Children = {
                _zoneEntryBox,
                _zoneExitBox,
                CreateLabeledControl("Zone Tag:", _zoneTagBox),
                CreateLabeledControl("Proximity Tag:", _proximityTagBox)
            }
        };

        mainPanel.Children.Add(CreateGroupBox("Waypoint Settings", waypointContent));
        mainPanel.Children.Add(CreateActionButtons(Save, Cancel));

        if (WaypointTemplate != null) {
            _zoneEntryBox.IsChecked = WaypointTemplate.m_zoneEntry;
            _zoneExitBox.IsChecked = WaypointTemplate.m_zoneExit;
            _zoneTagBox.Text = WaypointTemplate.m_zoneTag.ToString();
            _proximityTagBox.Text = WaypointTemplate.m_proximityTag.ToString();
        }
    }

    public Control GetTypeSpecificControls()
        => new StackPanel {
            Children = {
                _zoneEntryBox,
                _zoneExitBox,
                _zoneTagBox,
                _proximityTagBox
            }
        };

    public bool ValidateState() =>
        // At least one of zone entry/exit must be checked, and if proximity tag is used, it must not be empty.
        (_zoneEntryBox.IsChecked ?? false) || (_zoneExitBox.IsChecked ?? false) &&
        (string.IsNullOrWhiteSpace(_proximityTagBox.Text) || !string.IsNullOrWhiteSpace(_zoneTagBox.Text));

    public void SaveTypeSpecificValues() {
        WaypointTemplate.m_zoneEntry = _zoneEntryBox.IsChecked ?? false;
        WaypointTemplate.m_zoneExit = _zoneExitBox.IsChecked ?? false;
        WaypointTemplate.m_zoneTag = new ByteString(_zoneTagBox.Text ?? string.Empty);
        WaypointTemplate.m_proximityTag = new ByteString(_proximityTagBox.Text ?? string.Empty);
        // Note: m_goalType is set by the base class SaveValues() from the ComboBox selection
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