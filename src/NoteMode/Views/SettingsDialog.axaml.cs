using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using NoteMode.Services;

namespace NoteMode.Views;

public partial class SettingsDialog : Window
{
    private readonly FileAssociationService _service;
    private readonly Dictionary<string, CheckBox> _checkBoxes = new();

    public SettingsDialog()
    {
        InitializeComponent();
        _service = new FileAssociationService();
        if (this.FindControl<TextBlock>("VersionText") is { } version)
            version.Text = $"NoteMode {AppInfo.Version}" + (AppInfo.IsPackaged ? " (Microsoft Store)" : "");
        BuildUI();
    }

    private void BuildUI()
    {
        string? unavailable = null;
        if (!_service.IsWindows)
            unavailable = "File associations can only be set from here on Windows. Use your desktop's \"Open With\" settings instead.";
        else if (AppInfo.IsPackaged)
            unavailable = "This copy of NoteMode comes from the Microsoft Store. To open a file type with it, right-click a file > Open with > Choose another app, or use Windows Settings > Apps > Default apps.";

        if (unavailable != null)
        {
            if (this.FindControl<TextBlock>("NotAvailableText") is { } notAvailable)
            {
                notAvailable.Text = unavailable;
                notAvailable.IsVisible = true;
            }
            foreach (var name in new[] { "AssociationControls", "AssociationsHint" })
            {
                if (this.FindControl<Control>(name) is { } control)
                    control.IsVisible = false;
            }
            if (this.FindControl<Button>("ApplyButton") is { } apply)
                apply.IsVisible = false;
            return;
        }

        var container = this.FindControl<StackPanel>("ExtensionGroups");
        if (container == null) return;

        var extensions = _service.GetSupportedExtensions();
        string? currentCategory = null;
        WrapPanel? currentPanel = null;

        foreach (var ext in extensions)
        {
            if (ext.Category != currentCategory)
            {
                currentCategory = ext.Category;

                var header = new TextBlock
                {
                    Text = currentCategory,
                    FontSize = 12,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    Margin = new Avalonia.Thickness(0, 4, 0, 2)
                };
                container.Children.Add(header);

                currentPanel = new WrapPanel
                {
                    Orientation = Orientation.Horizontal
                };
                container.Children.Add(currentPanel);
            }

            var checkBox = new CheckBox
            {
                Content = ext.Extension,
                IsChecked = ext.IsAssociated,
                FontSize = 11,
                Margin = new Avalonia.Thickness(0, 0, 12, 4),
                MinWidth = 70
            };

            _checkBoxes[ext.Extension] = checkBox;
            currentPanel?.Children.Add(checkBox);
        }
    }

    private void SelectAll_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var cb in _checkBoxes.Values)
            cb.IsChecked = true;
    }

    private void DeselectAll_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var cb in _checkBoxes.Values)
            cb.IsChecked = false;
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        if (!_service.IsWindows || AppInfo.IsPackaged) return;

        foreach (var (ext, checkBox) in _checkBoxes)
        {
            if (checkBox.IsChecked == true)
                _service.SetAssociation(ext);
            else
                _service.RemoveAssociation(ext);
        }

        _service.NotifyShell();

        var count = _checkBoxes.Values.Count(cb => cb.IsChecked == true);
        if (this.FindControl<TextBlock>("StatusText") is { } status)
            status.Text = count == 0 ? "File associations removed." : $"Saved: {count} file type{(count == 1 ? "" : "s")} open with NoteMode.";
    }

    private async void OpenRepository_Click(object? sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new System.Uri(AppInfo.RepositoryUrl));
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}
