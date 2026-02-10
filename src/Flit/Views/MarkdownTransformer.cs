using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Flit.Views;

public partial class MarkdownTransformer : DocumentColorizingTransformer
{
    private double _baseFontSize = 14;
    private bool _isLightTheme;

    // Header multipliers: H1=2.0x, H2=1.6x, H3=1.3x, H4=1.1x, H5/H6=1.0x
    private static readonly double[] HeaderMultipliers = [2.0, 1.6, 1.3, 1.1, 1.0, 1.0];

    // --- Dark theme colors (Dracula-inspired) ---
    private static readonly SolidColorBrush DarkH1Color = new(Color.Parse("#bd93f9"));
    private static readonly SolidColorBrush DarkH2Color = new(Color.Parse("#ff79c6"));
    private static readonly SolidColorBrush DarkH3Color = new(Color.Parse("#8be9fd"));
    private static readonly SolidColorBrush DarkH4Color = new(Color.Parse("#50fa7b"));
    private static readonly SolidColorBrush DarkH5Color = new(Color.Parse("#ffb86c"));
    private static readonly SolidColorBrush DarkH6Color = new(Color.Parse("#f1fa8c"));
    private static readonly SolidColorBrush DarkCodeFg = new(Color.Parse("#50fa7b"));
    private static readonly SolidColorBrush DarkCodeBg = new(Color.Parse("#44475a"));
    private static readonly SolidColorBrush DarkBlockquoteColor = new(Color.Parse("#6272a4"));
    private static readonly SolidColorBrush DarkBulletColor = new(Color.Parse("#ff79c6"));
    private static readonly SolidColorBrush DarkLinkColor = new(Color.Parse("#8be9fd"));
    private static readonly SolidColorBrush DarkUrlColor = new(Color.Parse("#6272a4"));
    private static readonly SolidColorBrush DarkDimColor = new(Color.Parse("#6272a4"));
    private static readonly SolidColorBrush DarkStrikethroughColor = new(Color.Parse("#6272a4"));

    // --- Light theme colors ---
    private static readonly SolidColorBrush LightH1Color = new(Color.Parse("#6f42c1"));
    private static readonly SolidColorBrush LightH2Color = new(Color.Parse("#d63384"));
    private static readonly SolidColorBrush LightH3Color = new(Color.Parse("#0550ae"));
    private static readonly SolidColorBrush LightH4Color = new(Color.Parse("#1a7f37"));
    private static readonly SolidColorBrush LightH5Color = new(Color.Parse("#953800"));
    private static readonly SolidColorBrush LightH6Color = new(Color.Parse("#6e7781"));
    private static readonly SolidColorBrush LightCodeFg = new(Color.Parse("#0550ae"));
    private static readonly SolidColorBrush LightCodeBg = new(Color.Parse("#f0f0f0"));
    private static readonly SolidColorBrush LightBlockquoteColor = new(Color.Parse("#57606a"));
    private static readonly SolidColorBrush LightBulletColor = new(Color.Parse("#d63384"));
    private static readonly SolidColorBrush LightLinkColor = new(Color.Parse("#0969da"));
    private static readonly SolidColorBrush LightUrlColor = new(Color.Parse("#57606a"));
    private static readonly SolidColorBrush LightDimColor = new(Color.Parse("#6e7781"));
    private static readonly SolidColorBrush LightStrikethroughColor = new(Color.Parse("#6e7781"));

    // Header color arrays for easy indexing
    private static readonly SolidColorBrush[] DarkHeaderColors =
        [DarkH1Color, DarkH2Color, DarkH3Color, DarkH4Color, DarkH5Color, DarkH6Color];
    private static readonly SolidColorBrush[] LightHeaderColors =
        [LightH1Color, LightH2Color, LightH3Color, LightH4Color, LightH5Color, LightH6Color];

    // --- Compiled regex patterns ---

    // Horizontal rule: line is only ---, ***, or ___ (with optional spaces)
    [GeneratedRegex(@"^[ ]{0,3}([-*_])\s*\1\s*\1[\s\1]*$")]
    private static partial Regex HorizontalRuleRegex();

    // Headers: # through ######
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeaderRegex();

    // Blockquote: > at start of line
    [GeneratedRegex(@"^(\s*>+)\s?")]
    private static partial Regex BlockquoteRegex();

    // Unordered list bullets: -, *, + at start of line
    [GeneratedRegex(@"^(\s*[-*+])\s")]
    private static partial Regex UnorderedListRegex();

    // Ordered list markers: 1. 2. etc.
    [GeneratedRegex(@"^(\s*\d+[.)]\s)")]
    private static partial Regex OrderedListRegex();

    // Code span (backtick) - non-greedy, handles single and double backticks
    [GeneratedRegex(@"(`+)(.+?)\1")]
    private static partial Regex CodeSpanRegex();

    // Bold+italic: ***text*** or ___text___
    [GeneratedRegex(@"(\*{3}|_{3})(.+?)\1")]
    private static partial Regex BoldItalicRegex();

    // Bold: **text** or __text__
    [GeneratedRegex(@"(\*{2}|_{2})(.+?)\1")]
    private static partial Regex BoldRegex();

    // Italic: *text* or _text_ (not preceded/followed by same char)
    [GeneratedRegex(@"(?<![*_])([*_])(?![*_])(.+?)(?<![*_])\1(?![*_])")]
    private static partial Regex ItalicRegex();

    // Strikethrough: ~~text~~
    [GeneratedRegex(@"~~(.+?)~~")]
    private static partial Regex StrikethroughRegex();

    // Links: [text](url)
    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex LinkRegex();

    public void SetBaseFontSize(double size)
    {
        _baseFontSize = size;
    }

    public void SetLightTheme(bool isLight)
    {
        _isLightTheme = isLight;
    }

    private SolidColorBrush[] HeaderColors => _isLightTheme ? LightHeaderColors : DarkHeaderColors;
    private SolidColorBrush CodeFg => _isLightTheme ? LightCodeFg : DarkCodeFg;
    private SolidColorBrush CodeBg => _isLightTheme ? LightCodeBg : DarkCodeBg;
    private SolidColorBrush BlockquoteBrush => _isLightTheme ? LightBlockquoteColor : DarkBlockquoteColor;
    private SolidColorBrush BulletBrush => _isLightTheme ? LightBulletColor : DarkBulletColor;
    private SolidColorBrush LinkBrush => _isLightTheme ? LightLinkColor : DarkLinkColor;
    private SolidColorBrush UrlBrush => _isLightTheme ? LightUrlColor : DarkUrlColor;
    private SolidColorBrush DimBrush => _isLightTheme ? LightDimColor : DarkDimColor;
    private SolidColorBrush StrikethroughBrush => _isLightTheme ? LightStrikethroughColor : DarkStrikethroughColor;

    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.Length == 0)
            return;

        var lineText = CurrentContext.Document.GetText(line);
        var lineStart = line.Offset;

        // Horizontal rules: dim the entire line and return
        if (HorizontalRuleRegex().IsMatch(lineText))
        {
            ChangeLinePart(lineStart, lineStart + line.Length, e =>
            {
                e.TextRunProperties.SetForegroundBrush(DimBrush);
            });
            return;
        }

        // Headers: apply size, bold, and color to full line, then return
        var headerMatch = HeaderRegex().Match(lineText);
        if (headerMatch.Success)
        {
            var level = headerMatch.Groups[1].Value.Length; // 1-6
            var idx = level - 1;
            var multiplier = HeaderMultipliers[idx];
            var headerColor = HeaderColors[idx];
            var headerSize = _baseFontSize * multiplier;

            ChangeLinePart(lineStart, lineStart + line.Length, e =>
            {
                e.TextRunProperties.SetForegroundBrush(headerColor);
                var tf = e.TextRunProperties.Typeface;
                e.TextRunProperties.SetTypeface(new Typeface(tf.FontFamily, FontStyle.Normal, FontWeight.Bold));
                e.TextRunProperties.SetFontRenderingEmSize(headerSize);
            });
            return;
        }

        // Blockquote: color the > prefix
        var blockquoteMatch = BlockquoteRegex().Match(lineText);
        if (blockquoteMatch.Success)
        {
            var prefixEnd = lineStart + blockquoteMatch.Length;
            ChangeLinePart(lineStart, prefixEnd, e =>
            {
                e.TextRunProperties.SetForegroundBrush(BlockquoteBrush);
                var tf = e.TextRunProperties.Typeface;
                e.TextRunProperties.SetTypeface(new Typeface(tf.FontFamily, FontStyle.Italic, tf.Weight));
            });
            // Also italicize the rest of the line
            if (prefixEnd < lineStart + line.Length)
            {
                ChangeLinePart(prefixEnd, lineStart + line.Length, e =>
                {
                    e.TextRunProperties.SetForegroundBrush(BlockquoteBrush);
                    var tf = e.TextRunProperties.Typeface;
                    e.TextRunProperties.SetTypeface(new Typeface(tf.FontFamily, FontStyle.Italic, tf.Weight));
                });
            }
        }

        // List bullets (unordered)
        var ulMatch = UnorderedListRegex().Match(lineText);
        if (ulMatch.Success)
        {
            ChangeLinePart(lineStart + ulMatch.Groups[1].Index,
                lineStart + ulMatch.Groups[1].Index + ulMatch.Groups[1].Length, e =>
                {
                    e.TextRunProperties.SetForegroundBrush(BulletBrush);
                });
        }

        // List markers (ordered)
        var olMatch = OrderedListRegex().Match(lineText);
        if (olMatch.Success)
        {
            ChangeLinePart(lineStart + olMatch.Groups[1].Index,
                lineStart + olMatch.Groups[1].Index + olMatch.Groups[1].Length, e =>
                {
                    e.TextRunProperties.SetForegroundBrush(BulletBrush);
                });
        }

        // --- Inline formatting ---
        // Track claimed ranges from code spans to avoid processing bold/italic inside them
        var claimed = new List<(int Start, int End)>();

        // Code spans first (highest priority for overlap protection)
        foreach (Match m in CodeSpanRegex().Matches(lineText))
        {
            var start = lineStart + m.Index;
            var end = start + m.Length;
            claimed.Add((start, end));

            ChangeLinePart(start, end, e =>
            {
                e.TextRunProperties.SetForegroundBrush(CodeFg);
                e.TextRunProperties.SetBackgroundBrush(CodeBg);
            });
        }

        // Bold+italic: ***text*** or ___text___
        foreach (Match m in BoldItalicRegex().Matches(lineText))
        {
            var start = lineStart + m.Index;
            var end = start + m.Length;
            if (Overlaps(claimed, start, end)) continue;
            claimed.Add((start, end));

            ChangeLinePart(start, end, e =>
            {
                var tf = e.TextRunProperties.Typeface;
                e.TextRunProperties.SetTypeface(new Typeface(tf.FontFamily, FontStyle.Italic, FontWeight.Bold));
            });
        }

        // Bold: **text** or __text__
        foreach (Match m in BoldRegex().Matches(lineText))
        {
            var start = lineStart + m.Index;
            var end = start + m.Length;
            if (Overlaps(claimed, start, end)) continue;
            claimed.Add((start, end));

            ChangeLinePart(start, end, e =>
            {
                var tf = e.TextRunProperties.Typeface;
                e.TextRunProperties.SetTypeface(new Typeface(tf.FontFamily, tf.Style, FontWeight.Bold));
            });
        }

        // Italic: *text* or _text_
        foreach (Match m in ItalicRegex().Matches(lineText))
        {
            var start = lineStart + m.Index;
            var end = start + m.Length;
            if (Overlaps(claimed, start, end)) continue;
            claimed.Add((start, end));

            ChangeLinePart(start, end, e =>
            {
                var tf = e.TextRunProperties.Typeface;
                e.TextRunProperties.SetTypeface(new Typeface(tf.FontFamily, FontStyle.Italic, tf.Weight));
            });
        }

        // Strikethrough: ~~text~~
        foreach (Match m in StrikethroughRegex().Matches(lineText))
        {
            var start = lineStart + m.Index;
            var end = start + m.Length;
            if (Overlaps(claimed, start, end)) continue;

            ChangeLinePart(start, end, e =>
            {
                e.TextRunProperties.SetForegroundBrush(StrikethroughBrush);
            });
        }

        // Links: [text](url)
        foreach (Match m in LinkRegex().Matches(lineText))
        {
            var fullStart = lineStart + m.Index;
            if (Overlaps(claimed, fullStart, fullStart + m.Length)) continue;

            // Color the [text] part
            var textGroup = m.Groups[1];
            var textStart = lineStart + textGroup.Index;
            var textEnd = textStart + textGroup.Length;
            ChangeLinePart(textStart, textEnd, e =>
            {
                e.TextRunProperties.SetForegroundBrush(LinkBrush);
            });

            // Dim the (url) part
            var urlGroup = m.Groups[2];
            var urlStart = lineStart + urlGroup.Index;
            var urlEnd = urlStart + urlGroup.Length;
            ChangeLinePart(urlStart, urlEnd, e =>
            {
                e.TextRunProperties.SetForegroundBrush(UrlBrush);
            });
        }
    }

    private static bool Overlaps(List<(int Start, int End)> claimed, int start, int end)
    {
        foreach (var (cs, ce) in claimed)
        {
            if (start < ce && end > cs)
                return true;
        }
        return false;
    }
}
