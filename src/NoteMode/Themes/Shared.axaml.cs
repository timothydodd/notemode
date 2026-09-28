using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace NoteMode.Themes;

public partial class SharedStyles : Styles
{
    private static readonly string[] MonoCandidates =
        { "Consolas", "Cascadia Mono", "Menlo", "SF Mono", "DejaVu Sans Mono", "Liberation Mono", "Ubuntu Mono" };

    public SharedStyles()
    {
        AvaloniaXamlLoader.Load(this);

        // One installed font rather than a fallback list: on Linux, fontconfig "has" Consolas as an
        // alias it cannot load, and a list naming it crashes text layout (the Markdown preview).
        // Windows keeps Consolas, the editor font NoteMode has always used.
        Resources["MonoFont"] = new FontFamily(ResolveMonoFont());
    }

    private static string ResolveMonoFont()
    {
        try
        {
            var installed = FontManager.Current.SystemFonts
                .Select(f => f.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return MonoCandidates.FirstOrDefault(installed.Contains) ?? FontFamily.Default.Name;
        }
        catch
        {
            return FontFamily.Default.Name;
        }
    }
}
