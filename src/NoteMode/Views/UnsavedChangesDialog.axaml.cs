using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NoteMode.Views;

public enum UnsavedChangesResult
{
    Save,
    DontSave,
    Cancel
}

public partial class UnsavedChangesDialog : Window
{
    private TextBlock? _messageText;

    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    public UnsavedChangesDialog(string fileName) : this()
    {
        _messageText = this.FindControl<TextBlock>("MessageText");
        if (_messageText != null)
        {
            _messageText.Text = $"Do you want to save changes to \"{fileName}\"?";
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        Close(UnsavedChangesResult.Save);
    }

    private void DontSave_Click(object? sender, RoutedEventArgs e)
    {
        Close(UnsavedChangesResult.DontSave);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(UnsavedChangesResult.Cancel);
    }
}
