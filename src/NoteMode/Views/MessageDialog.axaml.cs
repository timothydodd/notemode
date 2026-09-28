using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NoteMode.Views;

/// <summary>A message with an OK button, e.g. why a file could not be opened or saved.</summary>
public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string message) : this()
    {
        Title = title;
        var messageText = this.FindControl<SelectableTextBlock>("MessageText");
        if (messageText != null)
            messageText.Text = message;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close();
}
