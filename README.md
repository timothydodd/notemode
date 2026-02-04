# Flit

A lightweight, fast desktop text editor built with C# and Avalonia UI.

## Features

- **Tabbed editing** with smart tab selection when closing
- **Syntax highlighting** for 30+ programming languages
- **Auto-save** with content caching (changes saved every 500ms)
- **Session persistence** - reopens your tabs and window state
- **Find & Replace** with regex support
- **Find in all tabs** search across open files
- **External file change detection** with reload prompts
- **Dracula dark theme** with clean, minimal UI
- **Cross-platform** - Windows, macOS, Linux

## Screenshots

<!-- Add screenshots here -->

## Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)

## Building

```bash
cd src
dotnet build
```

## Running

```bash
cd src
dotnet run --project Flit
```

Or for a release build:

```bash
cd src
dotnet build -c Release
dotnet run --project Flit -c Release
```

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+N` | New tab |
| `Ctrl+O` | Open file |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As |
| `Ctrl+Shift+A` | Save All |
| `Ctrl+W` | Close tab |
| `Ctrl+F` | Find |
| `Ctrl+H` | Find & Replace |
| `Ctrl+Shift+F` | Find in all tabs |
| `Ctrl+Shift+W` | Toggle whitespace |
| `Ctrl+Scroll` | Adjust font size |

## Architecture

Built with MVVM pattern:

```
src/Flit/
├── Models/          # Data structures (AppState, TabState)
├── Services/        # Business logic (state, cache, syntax)
├── ViewModels/      # Presentation logic with bindings
├── Views/           # Avalonia XAML UI definitions
└── Themes/          # Color schemes (Dracula)
```

## Data Storage

- App state: `~/.flit/state.json`
- Tab cache: `~/.flit/cache/`

## Tech Stack

- [Avalonia UI](https://avaloniaui.net/) - Cross-platform UI framework
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) - Text editor component
- [ReactiveUI](https://www.reactiveui.net/) - Reactive MVVM extensions

## License

MIT
