using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NoteMode.Views;

public enum FileChangedResult
{
    // First, so closing the window without a choice (default value) keeps the user's edits.
    KeepChanges,
    Reload
}

/// <summary>Asks what to do when a file with unsaved edits is changed on disk by another program.</summary>
public partial class FileChangedDialog : Window
{
    public FileChangedDialog()
    {
        InitializeComponent();
    }

    public FileChangedDialog(string fileName) : this()
    {
        var messageText = this.FindControl<TextBlock>("MessageText");
        if (messageText != null)
        {
            messageText.Text = $"\"{fileName}\" has been changed by another program.";
        }
    }

    private void Reload_Click(object? sender, RoutedEventArgs e)
    {
        Close(FileChangedResult.Reload);
    }

    private void KeepChanges_Click(object? sender, RoutedEventArgs e)
    {
        Close(FileChangedResult.KeepChanges);
    }
}
