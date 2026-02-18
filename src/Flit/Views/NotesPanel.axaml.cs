using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Flit.ViewModels;

namespace Flit.Views;

public partial class NotesPanel : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler<NoteTreeItemViewModel>? NoteOpened;

    private NoteTreeItemViewModel? _draggedItem;
    private NoteTreeItemViewModel? _currentDropTarget;
    private bool _isDragging;
    private Point _dragStartPoint;
    private const double DragThreshold = 8;

    public NotesPanel()
    {
        InitializeComponent();
    }

    private NotesPanelViewModel? ViewModel => DataContext as NotesPanelViewModel;

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NewFolder_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel?.CreateFolder(null);
    }

    private void NewSubFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is NoteTreeItemViewModel item && item.IsFolder)
        {
            ViewModel?.CreateFolder(item.Id);
        }
    }

    private void OpenNote_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is NoteTreeItemViewModel item && !item.IsFolder)
        {
            NoteOpened?.Invoke(this, item);
        }
    }

    private void DeleteItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is NoteTreeItemViewModel item)
        {
            ViewModel?.DeleteItem(item);
        }
    }

    private void RenameItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is NoteTreeItemViewModel item)
        {
            item.EditName = item.Name;
            item.IsEditing = true;
        }
    }

    private void RenameTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        CommitRename(sender as TextBox);
    }

    private void RenameTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitRename(sender as TextBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelRename(sender as TextBox);
            e.Handled = true;
        }
    }

    private void CommitRename(TextBox? textBox)
    {
        if (textBox?.DataContext is NoteTreeItemViewModel item && item.IsEditing)
        {
            item.IsEditing = false;
            var newName = item.EditName?.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != item.Name)
            {
                ViewModel?.RenameItem(item.Id, newName, item.IsFolder);
            }
        }
    }

    private void CancelRename(TextBox? textBox)
    {
        if (textBox?.DataContext is NoteTreeItemViewModel item)
        {
            item.IsEditing = false;
        }
    }

    private void TreeItem_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.Tag is NoteTreeItemViewModel item && !item.IsFolder)
        {
            NoteOpened?.Invoke(this, item);
            e.Handled = true;
        }
    }

    private void TreeItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is NoteTreeItemViewModel item)
        {
            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                _draggedItem = item;
                _dragStartPoint = e.GetPosition(this);
                _isDragging = false;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_draggedItem == null) return;

        var currentPoint = e.GetPosition(this);
        var diff = currentPoint - _dragStartPoint;

        if (!_isDragging && (Math.Abs(diff.X) > DragThreshold || Math.Abs(diff.Y) > DragThreshold))
        {
            _isDragging = true;
            DragGhost.IsVisible = true;
            DragGhostText.Text = (_draggedItem.IsFolder ? "\U0001F4C1 " : "\U0001F4C4 ") + _draggedItem.Name;
        }

        if (_isDragging)
        {
            // Position the drag ghost near the cursor
            DragGhost.Margin = new Thickness(currentPoint.X + 12, currentPoint.Y + 12, 0, 0);

            // Highlight drop target
            var target = FindHoverTarget(e);
            UpdateDropTarget(target);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        HandleDrop(e);
    }

    private void TreeItem_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        HandleDrop(e);
    }

    private void HandleDrop(PointerReleasedEventArgs e)
    {
        if (_isDragging && _draggedItem != null)
        {
            var target = FindHoverTarget(e);

            if (target != null && target != _draggedItem && target.IsFolder)
            {
                ViewModel?.MoveItem(_draggedItem, target);
            }
            else if (target == null)
            {
                // Dropped on root area
                ViewModel?.MoveItem(_draggedItem, null);
            }
        }

        ClearDragState();
    }

    private void ClearDragState()
    {
        UpdateDropTarget(null);
        DragGhost.IsVisible = false;
        _draggedItem = null;
        _isDragging = false;
    }

    private void UpdateDropTarget(NoteTreeItemViewModel? newTarget)
    {
        // Only highlight folders that aren't the dragged item itself
        if (newTarget != null && (!newTarget.IsFolder || newTarget == _draggedItem))
            newTarget = null;

        if (_currentDropTarget == newTarget) return;

        if (_currentDropTarget != null)
            _currentDropTarget.IsDropTarget = false;

        _currentDropTarget = newTarget;

        if (_currentDropTarget != null)
            _currentDropTarget.IsDropTarget = true;
    }

    private NoteTreeItemViewModel? FindHoverTarget(PointerEventArgs e)
    {
        var tree = this.FindControl<TreeView>("NotesTree");
        if (tree == null) return null;

        var treePoint = e.GetPosition(tree);
        var hit = tree.InputHitTest(treePoint);
        if (hit is Avalonia.Visual visual)
        {
            Avalonia.Visual? current = visual;
            while (current != null)
            {
                if (current is Border border && border.Tag is NoteTreeItemViewModel item)
                {
                    return item;
                }
                current = current.GetVisualParent();
            }
        }

        return null;
    }
}
