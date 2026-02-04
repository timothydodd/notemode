using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Flit.Views;

public enum FileChangedResult
{
    Reload,
    KeepChanges,
    Ignore
}

public partial class FileChangedDialog : Window
{
    private TextBlock? _messageText;
    private TextBlock? _conflictText;
    private Button? _keepChangesButton;

    public FileChangedDialog()
    {
        InitializeComponent();
    }

    public FileChangedDialog(string fileName, bool hasLocalChanges) : this()
    {
        _messageText = this.FindControl<TextBlock>("MessageText");
        _conflictText = this.FindControl<TextBlock>("ConflictText");
        _keepChangesButton = this.FindControl<Button>("KeepChangesButton");

        if (_messageText != null)
        {
            _messageText.Text = $"The file \"{fileName}\" has been modified outside of Flit.";
        }

        if (hasLocalChanges)
        {
            if (_conflictText != null)
            {
                _conflictText.IsVisible = true;
            }
            if (_keepChangesButton != null)
            {
                _keepChangesButton.IsVisible = true;
            }
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

    private void Ignore_Click(object? sender, RoutedEventArgs e)
    {
        Close(FileChangedResult.Ignore);
    }
}
