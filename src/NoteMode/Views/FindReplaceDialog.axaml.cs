using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit;
using NoteMode.ViewModels;

namespace NoteMode.Views;

public partial class FindReplaceDialog : Window
{
    private TextBox? _findTextBox;
    private TextBox? _replaceTextBox;
    private CheckBox? _caseSensitiveCheckBox;
    private TextBlock? _statusText;
    private TextEditor? _editor;
    private int _lastMatchIndex = -1;
    private SearchResultsBackgroundRenderer? _searchRenderer;

    public ICommand CloseCommand { get; }
    public ICommand FindNextCommand { get; }

    public FindReplaceDialog()
    {
        InitializeComponent();
        CloseCommand = new RelayCommand(_ => Close());
        FindNextCommand = new RelayCommand(_ => FindNext());
    }

    public FindReplaceDialog(TextEditor editor) : this()
    {
        _editor = editor;
        _findTextBox = this.FindControl<TextBox>("FindTextBox");
        _replaceTextBox = this.FindControl<TextBox>("ReplaceTextBox");
        _caseSensitiveCheckBox = this.FindControl<CheckBox>("CaseSensitiveCheckBox");
        _statusText = this.FindControl<TextBlock>("StatusText");

        // Add search results highlighter
        _searchRenderer = new SearchResultsBackgroundRenderer();
        _searchRenderer.SetLightTheme(App.Instance?.IsLightTheme ?? false);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_searchRenderer);

        // Pre-fill with selected text if any
        if (_editor != null && !string.IsNullOrEmpty(_editor.SelectedText))
        {
            _findTextBox!.Text = _editor.SelectedText;
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _findTextBox?.Focus();
        _findTextBox?.SelectAll();
        UpdateMatchCount();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Remove the search highlighter and clear highlights
        if (_editor != null && _searchRenderer != null)
        {
            _searchRenderer.ClearMatches();
            _editor.TextArea.TextView.BackgroundRenderers.Remove(_searchRenderer);
            _editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
        }
    }

    private void FindTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _lastMatchIndex = -1;
        UpdateMatchCount();
    }

    private void UpdateMatchCount()
    {
        if (_editor == null || _findTextBox == null || _statusText == null) return;

        var searchText = _findTextBox.Text;
        if (string.IsNullOrEmpty(searchText))
        {
            _statusText.Text = "";
            _searchRenderer?.ClearMatches();
            _editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
            return;
        }

        var comparison = IsCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var text = _editor.Text;
        var matches = new List<(int Start, int Length)>();
        int index = 0;

        while ((index = text.IndexOf(searchText, index, comparison)) != -1)
        {
            matches.Add((index, searchText.Length));
            index += searchText.Length;
        }

        _searchRenderer?.SetMatches(matches);
        _editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);

        _statusText.Text = matches.Count == 0 ? "No matches" : $"{matches.Count} match{(matches.Count == 1 ? "" : "es")}";
    }

    private bool IsCaseSensitive => _caseSensitiveCheckBox?.IsChecked == true;

    private void FindNext()
    {
        if (_editor == null || _findTextBox == null) return;

        var searchText = _findTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var comparison = IsCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var text = _editor.Text;
        int startIndex = _lastMatchIndex >= 0 ? _lastMatchIndex + 1 : _editor.CaretOffset;

        int index = text.IndexOf(searchText, startIndex, comparison);

        // Wrap around
        if (index == -1 && startIndex > 0)
        {
            index = text.IndexOf(searchText, 0, comparison);
        }

        if (index >= 0)
        {
            SelectMatch(index, searchText.Length);
            _lastMatchIndex = index;
        }
        else
        {
            UpdateStatus("No matches found");
        }
    }

    private void FindPrevious()
    {
        if (_editor == null || _findTextBox == null) return;

        var searchText = _findTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var comparison = IsCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var text = _editor.Text;
        int startIndex = _lastMatchIndex >= 0 ? _lastMatchIndex - 1 : _editor.CaretOffset - 1;

        if (startIndex < 0) startIndex = text.Length - 1;

        int index = text.LastIndexOf(searchText, startIndex, comparison);

        // Wrap around
        if (index == -1)
        {
            index = text.LastIndexOf(searchText, text.Length - 1, comparison);
        }

        if (index >= 0)
        {
            SelectMatch(index, searchText.Length);
            _lastMatchIndex = index;
        }
        else
        {
            UpdateStatus("No matches found");
        }
    }

    private void SelectMatch(int index, int length)
    {
        if (_editor == null) return;

        _editor.Select(index, length);
        _editor.CaretOffset = index + length;

        // Scroll to selection
        var location = _editor.Document.GetLocation(index);
        _editor.ScrollTo(location.Line, location.Column);
        _editor.Focus();
    }

    private void Replace()
    {
        if (_editor == null || _findTextBox == null || _replaceTextBox == null) return;

        var searchText = _findTextBox.Text;
        var replaceText = _replaceTextBox.Text ?? "";

        if (string.IsNullOrEmpty(searchText)) return;

        // If we have a selection that matches, replace it
        var comparison = IsCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (_editor.SelectedText.Equals(searchText, comparison))
        {
            var start = _editor.SelectionStart;
            _editor.Document.Replace(start, _editor.SelectionLength, replaceText);
            _editor.Select(start, replaceText.Length);
            _lastMatchIndex = start + replaceText.Length - 1;
            UpdateMatchCount();
        }

        // Find next occurrence
        FindNext();
    }

    private void ReplaceAll()
    {
        if (_editor == null || _findTextBox == null || _replaceTextBox == null) return;

        var searchText = _findTextBox.Text;
        var replaceText = _replaceTextBox.Text ?? "";

        if (string.IsNullOrEmpty(searchText)) return;

        var comparison = IsCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var text = _editor.Text;
        int count = 0;
        int index = 0;

        // Find all matches first
        while ((index = text.IndexOf(searchText, index, comparison)) != -1)
        {
            count++;
            index += searchText.Length;
        }

        if (count == 0)
        {
            UpdateStatus("No matches found");
            return;
        }

        // Replace all (from end to start to preserve indices)
        _editor.Document.BeginUpdate();
        try
        {
            index = text.Length;
            while (true)
            {
                index = text.LastIndexOf(searchText, index - 1, comparison);
                if (index < 0) break;
                _editor.Document.Replace(index, searchText.Length, replaceText);
                text = _editor.Text;
            }
        }
        finally
        {
            _editor.Document.EndUpdate();
        }

        UpdateStatus($"Replaced {count} occurrence{(count == 1 ? "" : "s")}");
        _lastMatchIndex = -1;
        UpdateMatchCount();
    }

    private void UpdateStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.Text = message;
        }
    }

    private void FindNext_Click(object? sender, RoutedEventArgs e)
    {
        FindNext();
    }

    private void FindPrevious_Click(object? sender, RoutedEventArgs e)
    {
        FindPrevious();
    }

    private void Replace_Click(object? sender, RoutedEventArgs e)
    {
        Replace();
    }

    private void ReplaceAll_Click(object? sender, RoutedEventArgs e)
    {
        ReplaceAll();
    }
}
