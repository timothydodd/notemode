# Release notes

One file per release, named after its tag (`v0.3.1.md`). The release workflow publishes the file as
the GitHub release description, and a `v*` tag build fails straight away if its file is missing, so
write it before tagging.

Each file should say:

1. **What NoteMode is**: one or two sentences for someone who lands on the release page cold.
2. **What's new**: user-facing changes, most important first, in plain language (what changed for
   the person using it, not which class moved). Call out anything that behaves differently than
   before and anything an upgrade does (e.g. removing the old MSI).
3. **Download**: which file to pick, and whether the build is code-signed.

GitHub appends the "Full Changelog" compare link automatically.
