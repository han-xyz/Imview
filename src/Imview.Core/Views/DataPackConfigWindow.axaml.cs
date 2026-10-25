using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Imview.Core.Common;
using Imview.Core.Data;

namespace Imview.Core.Views;

/// <summary>
/// Picks the data pack folder: on Save it writes <c>DataPack.Path</c> and reloads the world store, then closes with true.
/// </summary>
public partial class DataPackConfigWindow : Window {

    public DataPackConfigWindow() {
        InitializeComponent();
        PathBox.Text = ConfigurationManager.GetSetting(WorldStoreProvider.DataPackPathSetting) ?? string.Empty;
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e) {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
            Title = "Select the data pack folder",
            AllowMultiple = false
        });

        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (path != null) {
            PathBox.Text = path;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) {
        var path = PathBox.Text?.Trim() ?? string.Empty;
        if (!Directory.Exists(path)) {
            ErrorText.Text = "Choose an existing folder.";
            return;
        }

        try {
            ConfigurationManager.SetSetting(WorldStoreProvider.DataPackPathSetting, path);
        }
        catch (ArgumentException ex) {
            ErrorText.Text = ex.Message;
            return;
        }

        WorldStoreProvider.Reload();
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) {
        Close(false);
    }
}
