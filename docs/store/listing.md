# Microsoft Store listing

Copy for the Partner Center submission. Keep this file in sync with what is entered in Partner
Center so a resubmission does not start from scratch.

## Product

- **Name:** NoteMode (identity values go in the repository secrets `STORE_PACKAGE_NAME`,
  `STORE_PUBLISHER`, `STORE_PUBLISHER_DISPLAY` once the name is reserved)
- **Category:** Developer tools (or Productivity)
- **Pricing:** Free
- **Privacy policy URL:** https://raw.githubusercontent.com/timothydodd/notemode/main/docs/privacy.md
- **Support / website:** https://github.com/timothydodd/notemode

## Description

A lightweight, fast text editor that remembers your session.

Close NoteMode, restart your PC, come back next week: every tab is still open, including the ones
you never saved. NoteMode keeps unsaved edits safe on disk as you type, so there is no "Save
changes?" before you can shut down.

- **Session that survives everything.** Tabs, unsaved edits, window size, theme and panels come back
  exactly as you left them.
- **Notes without files.** Jot something down, give it a name, organise notes in folders. No Save
  dialog, no file to lose.
- **Search every open tab, or a whole folder,** with results in context and one click to the line.
- **Syntax highlighting** for 30+ languages, and a rendered preview for Markdown.
- **Find and replace, file explorer, whitespace and line numbers,** zoom with Ctrl+scroll.
- **Knows when a file changes on disk** and reloads it, or asks first if you have edits.
- **Keeps your encoding.** UTF-8 with or without BOM, UTF-16 and legacy ANSI files are saved the way
  they were opened.
- **Dracula dark and a clean light theme.**

NoteMode is free and open source, makes no network connections and collects no data.

## Short description (under 200 characters)

A fast, lightweight text editor that remembers every tab, even unsaved ones. Notes, search across
tabs, syntax highlighting and Markdown preview.

## Search terms

text editor, notepad, notepad++, notes, code editor, markdown, syntax highlighting, session restore

## Screenshots

Generate with `dotnet run --project tools/NoteMode.UiPreview -- --store docs/store/screenshots`
(render on Windows so the editor uses Consolas). Upload in this order:

1. `01-session.png` - Close it. Your tabs are still there.
2. `02-notes.png` - Notes that don't need a file
3. `03-search.png` - Search every tab, or a whole folder
4. `04-markdown.png` - Markdown, rendered
5. `05-explorer.png` - Light or dark, with the files beside you

Hero art: `docs/store/store-hero-1920x1080.png` and `store-hero-3840x2160.png`
(`-- --hero docs/store`). Store logo: `docs/store/store-icon-300x300.png`.

## Checklist

- [ ] Reserve the name in Partner Center and add the three identity secrets to the repository
- [ ] Tag a release; download the `NoteMode-msix` artifact from the workflow run
- [ ] Upload the package, screenshots, hero art and the copy above
- [ ] Privacy policy URL, support URL, age rating questionnaire (no user content shared, no network)
