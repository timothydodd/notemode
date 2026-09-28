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

Solution file: `src/NoteMode.sln` (the app plus `tools/NoteMode.UiPreview`)

```bash
# Render every window/dialog headlessly in both themes (no Windows needed) to check UI changes
dotnet run --project tools/NoteMode.UiPreview -- <outDir>
# Marketing screenshots / Store hero / MSIX logo images
dotnet run --project tools/NoteMode.UiPreview -- --store docs/store/screenshots
dotnet run --project tools/NoteMode.UiPreview -- --hero docs/store
dotnet run --project tools/NoteMode.UiPreview -- --assets packaging/Assets
```

The tool sets `NOTEMODE_HOME` to a temp folder, so it never touches the real `~/.notemode`.

## Architecture

**MVVM Pattern** with manual dependency injection:

- **Models/** - Data structures for state serialization (`AppState`, `TabState`)
- **Services/** - Business logic: `StateService` (JSON persistence), `CacheService` (tab content caching), `NoteService` (notes index), `SyntaxService` (language detection and highlighting), `TextFileIO` (encoding-preserving reads, safe replace-on-save), `FileChangeService` (polls open files for external changes), `AppPaths` (data folder, atomic writes), `AppInfo` (version, MSIX detection)
- **ViewModels/** - Presentation logic with `INotifyPropertyChanged` bindings and `RelayCommand` for commands
- **Views/** - Avalonia XAML (`.axaml`) UI definitions

**Data Flow:**
```
User Input → MainWindow Events → MainWindowViewModel Commands
  → Services → TabViewModel State → EditorView UI
```

**State Persistence** (folder overridable with `NOTEMODE_HOME`):
- App state: `~/.notemode/state.json` (window size, font size, active tab, tabs list)
- Notes index: `~/.notemode/notes.json`
- Tab content cache: `~/.notemode/cache/{TabId}.cache` (auto-saved 500ms after typing; flushed on exit and on a crash)
- Crash log: `~/.notemode/error.log`

**Data-safety rules** (users rely on the session never losing text):
- Guards that decide whether a tab can be closed/reloaded use `HasUnsavedChanges` (or call `EnsureContentLoaded()` first): a restored tab that hasn't been shown has cached edits but `IsDirty == false` until loaded.
- `SaveFile` returns false (and raises `ErrorOccurred`) on failure; never close a tab or delete its cache after a failed save.
- Dialog result enums put the safe choice first (`Cancel`, `KeepChanges`), because closing a dialog with its X returns `default`.
- File I/O for user files goes through `TextFileIO` so the original encoding is kept.

## Key Dependencies

- Avalonia 12.1.x - Cross-platform UI framework
- Avalonia.AvaloniaEdit 12.0.0 - Text editor component
- Markdown.Avalonia.Tight - Markdown preview

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

## Packaging and releases

Same setup as the RoboMouse repo (see `docs/building.md`):
- `packaging/installer/NoteMode.iss` (Inno Setup 6) built by `packaging/Build-Installer.ps1`; smoke test `packaging/Test-Installer.ps1`
- `packaging/Build-Msix.ps1` + `packaging/Package.appxmanifest` for the Microsoft Store (file types declared in the manifest; Settings hides registry associations when `AppInfo.IsPackaged`)
- `.github/workflows/build.yml`: build on push/PR; `v*` tags build the installer + MSIX, sign with Azure Trusted Signing (when configured), smoke-test the installer, then publish the GitHub release. The version comes from the tag; bump `<Version>` in `NoteMode.csproj` when tagging.
- **Every release needs `docs/releases/<tag>.md`** (what NoteMode is, what's new in plain user-facing language, downloads/signing) committed before the tag is pushed; CI publishes it as the release description and fails the tag build without it. When editing an existing release, update both the file and the published body (`gh release edit <tag> --notes-file docs/releases/<tag>.md`).
- Tag builds pause for the owner's approval (`release` environment, required reviewer, `v*` tags only) before anything is signed or published.

## Theming

Two themes, `Themes/Dracula.axaml` and `Themes/Light.axaml`, swapped at runtime by `App.ApplyTheme`. Theme-independent structural styles live in `Themes/Shared.axaml` (window title-bar buttons `Button.winctl`, side panel headers `Border.panelHeader` / `TextBlock.panelTitle` / `Path.panelIcon` / `Button.panelClose`, `TextBox.compact`, `Button.statusItem`) and are re-added after the theme so they win. Colors always come from theme resources (`{DynamicResource ...}`); add any new color to **both** themes. The editor/code font is the `MonoFont` resource, resolved at startup to one installed font (never use a font-family fallback list: the Markdown preview crashes on one whose first family fails to load).

Dracula colors:
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

`MainWindow.axaml` KeyBindings, plus `MainWindow.OnPreviewKeyDown` (tunnel) for keys the editor would otherwise swallow (Ctrl+S, Ctrl+Shift+S, Ctrl+Shift+F, Ctrl+E, Ctrl+Shift+V, Ctrl+Shift+N) - define each shortcut in one place only:
- `Ctrl+N` New tab, `Ctrl+O` Open, `Ctrl+S` Save, `Ctrl+Shift+S` Save As
- `Ctrl+Shift+A` Save All, `Ctrl+W` Close tab, `Ctrl+Shift+W` Toggle whitespace
- `Ctrl+F` Find, `Ctrl+H` Replace, `Ctrl+Shift+F` Search panel, `Ctrl+E` Explorer, `Ctrl+Shift+N` Notes, `Ctrl+Shift+V` Markdown preview
- `Ctrl+Scroll` Font size (6-72pt, handled in `EditorView.axaml.cs`)
