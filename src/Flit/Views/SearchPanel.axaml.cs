using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Flit.ViewModels;

namespace Flit.Views;

public partial class SearchPanel : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler? BrowseFolderRequested;
    public event EventHandler<SearchResultItem>? ResultSelected;

    public SearchPanel()
    {
        InitializeComponent();
    }

    public void FocusSearchBox()
    {
        var searchBox = this.FindControl<TextBox>("SearchTextBox");
        searchBox?.Focus();
        searchBox?.SelectAll();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BrowseFolder_Click(object? sender, RoutedEventArgs e)
    {
        BrowseFolderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ResultItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is SearchResultItem item)
        {
            ResultSelected?.Invoke(this, item);
            e.Handled = true;
        }
    }
}
