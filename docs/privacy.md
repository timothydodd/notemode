# NoteMode privacy policy

NoteMode does not collect, send or share any data. It makes no network connections.

Everything it stores stays on your computer, in your user profile:

- `~/.notemode/state.json`: open tabs, window size and position, panel and theme settings
- `~/.notemode/notes.json`: your notes' titles and folders
- `~/.notemode/cache/`: the text of your notes and of unsaved edits, so they survive a restart
- `~/.notemode/error.log`: details of unexpected errors, only if one happens

NoteMode reads and writes only the files you open or save. File associations you choose in
Settings are written to your own Windows user registry and removed by the uninstaller.

Uninstalling NoteMode leaves `~/.notemode` in place so your notes are not lost; delete that folder
to remove them.

Questions: https://github.com/timothydodd/notemode/issues
