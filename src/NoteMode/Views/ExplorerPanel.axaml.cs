using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NoteMode.ViewModels;

namespace NoteMode.Views;

public partial class ExplorerPanel : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler<string>? FileOpened;

    public ExplorerPanel()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TreeItem_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.Tag is FileTreeItemViewModel item)
        {
            if (!item.IsDirectory && !string.IsNullOrEmpty(item.FullPath))
            {
                FileOpened?.Invoke(this, item.FullPath);
                e.Handled = true;
            }
        }
    }
}
