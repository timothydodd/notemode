using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using NoteMode.Services;
using NoteMode.ViewModels;

namespace NoteMode.Views;

public class LanguageItem
{
    public string Name { get; set; } = "";
    public string[] Extensions { get; set; } = Array.Empty<string>();
    public string ExtensionsDisplay => Extensions.Length > 0 ? string.Join(", ", Extensions) : "";
}

public partial class LanguagePickerDialog : Window
{
    private readonly List<LanguageItem> _allLanguages;
    private TextBox? _searchTextBox;
    private ListBox? _languageListBox;

    public ICommand CloseCommand { get; }
    public ICommand SelectCommand { get; }

    public string? SelectedLanguage { get; private set; }

    public LanguagePickerDialog()
    {
        InitializeComponent();
        _allLanguages = new List<LanguageItem>();
        CloseCommand = new RelayCommand(_ => Close());
        SelectCommand = new RelayCommand(_ => SelectAndClose());
    }

    public LanguagePickerDialog(SyntaxService syntaxService, string? currentLanguage = null) : this()
    {
        _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
        _languageListBox = this.FindControl<ListBox>("LanguageListBox");

        // Load all languages
        _allLanguages.AddRange(syntaxService.GetAllLanguages().Select(l => new LanguageItem
        {
            Name = l.Name,
            Extensions = l.Extensions
        }));

        UpdateFilteredList("");

        // Select current language
        if (!string.IsNullOrEmpty(currentLanguage) && _languageListBox != null)
        {
            var current = _allLanguages.FirstOrDefault(l =>
                l.Name.Equals(currentLanguage, StringComparison.OrdinalIgnoreCase));
            if (current != null)
            {
                _languageListBox.SelectedItem = current;
                _languageListBox.ScrollIntoView(current);
            }
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _searchTextBox?.Focus();
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var searchText = _searchTextBox?.Text ?? "";
        UpdateFilteredList(searchText);
    }

    private void UpdateFilteredList(string searchText)
    {
        if (_languageListBox == null) return;

        IEnumerable<LanguageItem> filtered;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            filtered = _allLanguages;
        }
        else
        {
            var search = searchText.Trim();
            filtered = _allLanguages.Where(l =>
                l.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                l.Extensions.Any(ext => ext.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        _languageListBox.ItemsSource = filtered.ToList();

        // Auto-select first item
        if (_languageListBox.ItemCount > 0)
        {
            _languageListBox.SelectedIndex = 0;
        }
    }

    private void LanguageListBox_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        SelectAndClose();
    }

    private void LanguageListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Keep track of selection
    }

    private void SelectAndClose()
    {
        if (_languageListBox?.SelectedItem is LanguageItem item)
        {
            SelectedLanguage = item.Name;
            Close(SelectedLanguage);
        }
    }
}
