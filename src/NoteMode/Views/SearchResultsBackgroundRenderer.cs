using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace NoteMode.Views;

public class SearchResultsBackgroundRenderer : IBackgroundRenderer
{
    private readonly List<(int Start, int Length)> _matches = new();

    // Dark theme: brownish highlight
    private static readonly IBrush DarkBrush = new SolidColorBrush(Color.Parse("#5A4020"));
    // Light theme: yellow highlight
    private static readonly IBrush LightBrush = new SolidColorBrush(Color.Parse("#FFFF00"));

    private IBrush _brush = DarkBrush;

    public KnownLayer Layer => KnownLayer.Background;

    public void SetLightTheme(bool isLight)
    {
        _brush = isLight ? LightBrush : DarkBrush;
    }

    public void SetMatches(IEnumerable<(int Start, int Length)> matches)
    {
        _matches.Clear();
        _matches.AddRange(matches);
    }

    public void ClearMatches()
    {
        _matches.Clear();
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_matches.Count == 0) return;

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0) return;

        foreach (var match in _matches)
        {
            var segment = new TextSegment { StartOffset = match.Start, Length = match.Length };

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.FillRectangle(_brush, rect);
            }
        }
    }
}
