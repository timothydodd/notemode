using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Flit.Views;

public partial class RenameDialog : Window
{
    private TextBox? _nameTextBox;

    public RenameDialog()
    {
        InitializeComponent();
    }

    public RenameDialog(string currentName) : this()
    {
        _nameTextBox = this.FindControl<TextBox>("NameTextBox");
        if (_nameTextBox != null)
        {
            _nameTextBox.Text = currentName;
        }
    }

    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        _nameTextBox?.Focus();
        _nameTextBox?.SelectAll();
    }

    private void OK_Click(object? sender, RoutedEventArgs e)
    {
        Close(_nameTextBox?.Text);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
