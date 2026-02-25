using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using NoteMode.ViewModels;

namespace NoteMode.Views;

public class SearchResult
{
    public required TabViewModel Tab { get; init; }
    public required string TabTitle { get; init; }
    public required int LineNumber { get; init; }
    public required int ColumnNumber { get; init; }
    public required string LinePreview { get; init; }
    public required int StartOffset { get; init; }
    public required int Length { get; init; }
}

public partial class FindInTabsDialog : Window
{
    private TextBox? _searchTextBox;
    private CheckBox? _caseSensitiveCheckBox;
    private TextBlock? _statusText;
    private ListBox? _resultsListBox;
    private readonly MainWindowViewModel _viewModel;
    private readonly Action<TabViewModel, int, int, int> _goToResult;
    private readonly ObservableCollection<SearchResult> _results = new();
    private DispatcherTimer? _debounceTimer;

    public ICommand CloseCommand { get; }
    public ICommand GoToSelectedCommand { get; }

    public FindInTabsDialog()
    {
        InitializeComponent();
        CloseCommand = new RelayCommand(_ => Close());
        GoToSelectedCommand = new RelayCommand(_ => GoToSelected());
        _viewModel = null!;
        _goToResult = null!;
    }

    public FindInTabsDialog(MainWindowViewModel viewModel, Action<TabViewModel, int, int, int> goToResult) : this()
    {
        _viewModel = viewModel;
        _goToResult = goToResult;

        _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
        _caseSensitiveCheckBox = this.FindControl<CheckBox>("CaseSensitiveCheckBox");
        _statusText = this.FindControl<TextBlock>("StatusText");
        _resultsListBox = this.FindControl<ListBox>("ResultsListBox");

        if (_resultsListBox != null)
        {
            _resultsListBox.ItemsSource = _results;
        }

        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            PerformSearch();
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _searchTextBox?.Focus();
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    private void CaseSensitiveCheckBox_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PerformSearch();
    }

    private bool IsCaseSensitive => _caseSensitiveCheckBox?.IsChecked == true;

    private void PerformSearch()
    {
        _results.Clear();

        var searchText = _searchTextBox?.Text;
        if (string.IsNullOrEmpty(searchText) || searchText.Length < 2)
        {
            UpdateStatus("");
            return;
        }

        var comparison = IsCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        int totalMatches = 0;
        int tabsWithMatches = 0;

        foreach (var tab in _viewModel.Tabs)
        {
            var content = tab.Content ?? "";
            var lines = content.Split('\n');
            int offset = 0;
            bool tabHasMatch = false;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                int columnIndex = 0;

                while ((columnIndex = line.IndexOf(searchText, columnIndex, comparison)) != -1)
                {
                    if (!tabHasMatch)
                    {
                        tabHasMatch = true;
                        tabsWithMatches++;
                    }

                    totalMatches++;

                    var preview = line.Trim();
                    if (preview.Length > 80)
                    {
                        preview = preview.Substring(0, 80) + "...";
                    }

                    _results.Add(new SearchResult
                    {
                        Tab = tab,
                        TabTitle = tab.Title,
                        LineNumber = lineIndex + 1,
                        ColumnNumber = columnIndex + 1,
                        LinePreview = preview,
                        StartOffset = offset + columnIndex,
                        Length = searchText.Length
                    });

                    columnIndex += searchText.Length;
                }

                offset += line.Length + 1; // +1 for newline
            }
        }

        if (totalMatches == 0)
        {
            UpdateStatus("No matches found");
        }
        else
        {
            UpdateStatus($"{totalMatches} match{(totalMatches == 1 ? "" : "es")} in {tabsWithMatches} tab{(tabsWithMatches == 1 ? "" : "s")}");
        }
    }

    private void UpdateStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.Text = message;
        }
    }

    private void GoToSelected()
    {
        if (_resultsListBox?.SelectedItem is SearchResult result)
        {
            _goToResult(result.Tab, result.StartOffset, result.Length, result.LineNumber);
            Close();
        }
    }

    private void ResultsListBox_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        GoToSelected();
    }
}
