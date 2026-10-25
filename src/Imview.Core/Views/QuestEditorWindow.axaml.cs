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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Imcodec.ObjectProperty.TypeCache;
using Imview.Core.Common.Constants;
using Imview.Core.Services;
using System;
using System.Threading.Tasks;
using Imview.Core.Common.Extensions;

namespace Imview.Core.Views;

public partial class QuestEditorWindow : Avalonia.Controls.Window {

    private readonly QuestTemplate? _originalTemplate;

    public QuestTemplate EditedTemplate { get; private set; }

    public QuestEditorWindow() {
        InitializeComponent();

        _originalTemplate = null;
        EditedTemplate = new QuestTemplate();

        Height = EditorConstants.DEFAULT_WINDOW_HEIGHT;
        Width = EditorConstants.DEFAULT_WINDOW_WIDTH;
    }

    public QuestEditorWindow(QuestTemplate template, bool isReadOnly = false) {
        _originalTemplate = template;
        EditedTemplate = template;
        
        InitializeComponent();
        InitializeEditor(isReadOnly);
    }

    private void InitializeComponent() {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeEditor(bool isReadOnly = false) {
        var editor = this.FindControl<Controls.Templates.QuestTemplateEditor>("QuestEditor");
        if (editor != null && _originalTemplate != null) {
            editor.Template = _originalTemplate;
            
            if (isReadOnly) {
                MakeEditorReadOnly(editor);
                Title = $"View Quest: {_originalTemplate.m_questTitle.OrIfEmpty(_originalTemplate.m_questName)}";
            } else {
                Title = $"Edit Quest: {_originalTemplate.m_questTitle.OrIfEmpty(_originalTemplate.m_questName)}";
            }
        }
    }
    
    private void MakeEditorReadOnly(Controls.Templates.QuestTemplateEditor editor) {
        DisableEditableControls(editor);
    }
    
    private void DisableEditableControls(Control control) {
        switch (control) {
            case TextBox textBox:
                textBox.IsReadOnly = true;
                break;
            case NumericUpDown numericUpDown:
                numericUpDown.IsEnabled = false;
                break;
            case ComboBox comboBox:
                comboBox.IsEnabled = false;
                break;
            case CheckBox checkBox:
                checkBox.IsEnabled = false;
                break;
            case Avalonia.Controls.Button button:
                if (button.Content is string content && content != "Close") {
                    button.IsEnabled = false;
                }
                break;
        }
        
        // Recursively process child controls
        if (control is Panel panel) {
            foreach (var child in panel.Children) {
                if (child is Control childControl) {
                    DisableEditableControls(childControl);
                }
            }
        } else if (control is ContentControl contentControl && contentControl.Content is Control childContent) {
            DisableEditableControls(childContent);
        } else if (control is ItemsControl itemsControl) {
            foreach (var item in itemsControl.GetLogicalChildren()) {
                if (item is Control childControl) {
                    DisableEditableControls(childControl);
                }
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

}