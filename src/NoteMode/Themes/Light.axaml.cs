using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace NoteMode.Themes;

public partial class LightTheme : Styles
{
    public LightTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
