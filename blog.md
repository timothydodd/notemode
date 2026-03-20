# NoteMode: An Open-Source Notepad++ Alternative After the Security Incident

Notepads and text editors have been around forever, and I never really thought about creating one. The original Visual Studio even had a template that was basically a notepad program. I use VS Code and Visual Studio for coding, but I've always kept a supplemental editor around for taking quick notes or viewing files without any extra overhead.

I've tried Notepad, Sublime Text, but Notepad++ has always been my go-to. The feature I love most is that it keeps files open even after you close it—your session is right there when you come back.

After the recent security incident with Notepad++, I decided to build my own alternative. With today's tools, it's relatively straightforward, and I like knowing the source code is visible and customizable to exactly how I want it.

With that said, I want to introduce NoteMode.

## What is NoteMode?

NoteMode is a fast, lightweight text editor built for developers who want simplicity without sacrificing essential features. Written in C# with Avalonia UI, it runs natively on Windows, macOS, and Linux with a clean, distraction-free interface. NoteMode remembers your session between restarts, auto-saves your work, and stays out of your way while you code.

Light and dark screenshot of UI

## Key Features

### Lightweight & Fast

- Minimal footprint with instant startup
- Native cross-platform performance (Windows, macOS, Linux)

### Session Persistence

- Automatically restores your tabs, window size, and font preferences on restart
- Unsaved changes are cached and recovered if you close without saving

### Syntax Highlighting

- Support for 30+ languages including C#, JavaScript, TypeScript, Python, HTML, CSS, SQL, YAML, and more
- Manual language override that persists between sessions
- Clickable language selector in the status bar

### Dual Theme Support

- Dracula dark theme for low-light coding
- Clean light theme inspired by VS Code
- Live theme switching without restarting

### Tab Management

- Drag-and-drop tab reordering with visual drag ghost
- Visual dirty indicator for unsaved changes
- External file change detection with reload prompt
- Smart tab selection when closing (selects next or previous tab)
- Tab context menu: Close Others, Close to Right, Close to Left, Close Unchanged
- Rename tabs from the context menu

### Notes Panel

- Built-in notes system for quick note-taking that lives outside your project files
- Organize notes into a hierarchical folder structure
- Drag-and-drop notes between folders
- Convert any open file into a persistent note with "Save as Note"
- Notes persist across sessions in ~/.notemode/

### Search & Replace

- Find and replace within files with regex support
- Search panel (Ctrl+Shift+F) with two modes: search across open tabs or search files on disk
- File search scans directories with smart filtering (skips .git, node\_modules, bin, obj, etc.)
- Case sensitivity and whole word matching options
- Click results to jump directly to the matching line

### Markdown Support

- Live markdown rendering with styled headers, bold, italic, strikethrough, code spans, blockquotes, lists, and links
- Theme-aware formatting that adapts to dark and light themes

### Keyboard-Driven Workflow

- Standard shortcuts: Ctrl+N/O/S/W, Ctrl+Z/Y, Ctrl+F/H
- Ctrl+Scroll to adjust font size (6-72pt)
- Ctrl+Shift+F to toggle search panel
- Ctrl+Shift+N to toggle notes panel
- Ctrl+Shift+A to save all open tabs

### Status Bar

- Line and column indicator
- File encoding and line ending display (CRLF/LF/CR)
- Zoom percentage control
- Clickable syntax language selector

## Get NoteMode

NoteMode is open source. You can find the source code and releases on GitHub:

https://github.com/timothydodd/notemode/releases
