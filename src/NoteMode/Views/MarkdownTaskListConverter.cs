using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace NoteMode.Views;

/// <summary>
/// Markdown.Avalonia's parser does not support GitHub-flavored task lists
/// (<c>- [ ]</c> / <c>- [x]</c>), so it renders the brackets as literal text.
/// This converter rewrites the checkbox marker of list items into ballot-box
/// glyphs (☐ / ☑) for display only — the underlying file content is untouched.
/// </summary>
public partial class MarkdownTaskListConverter : IValueConverter
{
    public static readonly MarkdownTaskListConverter Instance = new();

    private const string Unchecked = "☐"; // ☐ BALLOT BOX
    private const string Checked = "☑";   // ☑ BALLOT BOX WITH CHECK

    // Matches a list item whose first content is a [ ] / [x] checkbox marker.
    [GeneratedRegex(@"(?m)^(?<lead>[ \t]*[-*+][ \t]+)\[(?<mark>[ xX])\](?<trail>[ \t])")]
    private static partial Regex TaskListRegex();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string markdown || markdown.Length == 0)
            return value;

        return TaskListRegex().Replace(markdown, static m =>
        {
            var glyph = m.Groups["mark"].Value == " " ? Unchecked : Checked;
            return m.Groups["lead"].Value + glyph + m.Groups["trail"].Value;
        });
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
