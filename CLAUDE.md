# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NoteMode is a lightweight desktop text editor built with C# (.NET 10.0) and Avalonia UI framework using MVVM architecture.

## Build Commands

```bash
# Build (from src/ directory)
cd src
dotnet build

# Run the application
dotnet run --project NoteMode

# Release build
dotnet build -c Release
```

Solution file: `src/NoteMode.sln`

## Architecture

**MVVM Pattern** with manual dependency injection:

- **Models/** - Data structures for state serialization (`AppState`, `TabState`)
- **Services/** - Business logic: `StateService` (JSON persistence), `CacheService` (tab content caching), `SyntaxService` (language detection and highlighting)
- **ViewModels/** - Presentation logic with `INotifyPropertyChanged` bindings and `RelayCommand` for commands
- **Views/** - Avalonia XAML (`.axaml`) UI definitions

**Data Flow:**
```
User Input → MainWindow Events → MainWindowViewModel Commands
  → Services → TabViewModel State → EditorView UI
```

**State Persistence:**
- App state: `~/.notemode/state.json` (window size, font size, active tab, tabs list)
- Tab content cache: `~/.notemode/cache/{TabId}.cache` (auto-saved every 500ms)

## Key Dependencies

- Avalonia 11.3.x - Cross-platform UI framework
- Avalonia.AvaloniaEdit 11.4.0 - Text editor component
- ReactiveUI.Avalonia - Reactive extensions for MVVM

## Entry Points

1. `Program.cs` - Avalonia app builder setup
2. `App.axaml.cs` - Service creation, ViewModel initialization, shutdown handling
3. `MainWindow.axaml.cs` - UI events, file dialogs, drag-and-drop

## Tab & Content Model

Each tab (`TabViewModel`) has:
- **Dirty state tracking**: Compares current content vs. original, shows "●" indicator when modified
- **Auto-caching**: Content saved to disk 500ms after changes (debounced)
- **Syntax highlighting**: Applied based on file extension via `SyntaxService` (30+ languages supported)

Smart tab selection: When closing a tab, selects next tab to the right, or previous if rightmost.

## Theming

Uses Dracula color scheme defined in `Themes/Dracula.axaml`:
- Background: `#282a36`, Title bar: `#21222c`, Foreground: `#f8f8f2`
- Syntax colors set in `SyntaxService.cs` (comments gray, strings yellow, keywords pink, etc.)

### Icons — Lucide
- All UI icons (except the app icon) use **Lucide** SVG paths from https://github.com/lucide-icons/lucide/tree/main/icons
- Icons are **stroke-based**: use `Stroke`, `StrokeThickness="1.5"`, `StrokeLineCap="Round"`, `StrokeJoin="Round"` — never `Fill` for icons. (Avalonia uses `StrokeJoin`, not `StrokeLineJoin`.)
- Lucide SVGs use a 24×24 viewBox. Multi-element SVGs (multiple `<path>`, `<circle>`, `<rect>`, `<ellipse>`) must be combined into a single `Data` string:
  - Concatenate multiple `<path d="..."/>` with spaces: `"M3 12h18 M12 3v18"`
  - Convert `<circle cx="12" cy="12" r="10"/>` → `M2 12a10 10 0 1 0 20 0a10 10 0 1 0-20 0`
  - Convert `<ellipse cx="12" cy="5" rx="9" ry="3"/>` → `M3 5a9 3 0 1 0 18 0a9 3 0 1 0-18 0`
  - Convert `<rect x="3" y="3" width="18" height="18" rx="2"/>` → `M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z`
- When adding new icons, find the matching Lucide icon at the URL above, fetch its SVG, and convert using the rules above.

## Keyboard Shortcuts

Defined in `MainWindow.axaml`:
- `Ctrl+N` New tab, `Ctrl+O` Open, `Ctrl+S` Save, `Ctrl+Shift+S` Save As
- `Ctrl+Shift+A` Save All, `Ctrl+W` Close tab, `Ctrl+Shift+W` Toggle whitespace
- `Ctrl+Scroll` Font size (6-72pt, handled in `EditorView.axaml.cs`)
