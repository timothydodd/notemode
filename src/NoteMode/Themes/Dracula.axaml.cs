using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace NoteMode.Themes;

public partial class DraculaTheme : Styles
{
    public DraculaTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
