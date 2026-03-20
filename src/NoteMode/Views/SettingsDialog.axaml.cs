using System.Collections.Generic;
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
        BuildUI();
    }

    private void BuildUI()
    {
        if (!_service.IsWindows)
        {
            var notAvailable = this.FindControl<TextBlock>("NotAvailableText");
            if (notAvailable != null)
                notAvailable.IsVisible = true;

            var controls = this.FindControl<StackPanel>("AssociationControls");
            if (controls != null)
                controls.IsVisible = false;

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
        if (!_service.IsWindows) return;

        foreach (var (ext, checkBox) in _checkBoxes)
        {
            if (checkBox.IsChecked == true)
                _service.SetAssociation(ext);
            else
                _service.RemoveAssociation(ext);
        }

        _service.NotifyShell();
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
