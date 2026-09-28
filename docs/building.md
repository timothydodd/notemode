# Building and releasing

## Build and run

```bash
git clone https://github.com/timothydodd/notemode.git
cd notemode
dotnet build src/NoteMode.sln
dotnet run --project src/NoteMode
```

A normal (JIT) build is fine for development and runs on Windows, macOS and Linux. Needs the
[.NET 10 SDK](https://dotnet.microsoft.com/download).

To check UI changes without running the app, `tools/NoteMode.UiPreview` renders every window and
dialog headlessly, in both themes, from a sample session (it never touches your own `~/.notemode`):

```bash
dotnet run --project tools/NoteMode.UiPreview -- <outDir>
```

## Solution layout

```
src/NoteMode/           The app (Avalonia, MVVM, Native AOT)
tools/NoteMode.UiPreview/  Renders the UI to PNG on any OS: previews, marketing screenshots, logo assets
packaging/              Inno Setup script, MSIX manifest and assets, build and test scripts
docs/                   This file, the privacy policy, Store listing copy and screenshots
```

## Native AOT publish

```bash
dotnet publish src/NoteMode -c Release -r win-x64
```

The app has `PublishAot` enabled, so this produces a native `NoteMode.exe` that needs no .NET
runtime. It must run on Windows with the Visual Studio "Desktop development with C++" workload (the
AOT compiler needs the MSVC linker).

## Installer

`packaging/Build-Installer.ps1` publishes the app and compiles `packaging/installer/NoteMode.iss`
with Inno Setup 6 into `artifacts/NoteMode-Setup-<ver>.exe`:

```powershell
.\packaging\Build-Installer.ps1 -Version 0.3.0
```

The installer is per machine (Program Files\NoteMode, the same folder the old MSI used, so file
associations made in Settings keep working). It adds a Start menu shortcut, an optional desktop
shortcut, "Edit with NoteMode" / "Open NoteMode here" in the Explorer context menu (a task the user
can untick) and lists NoteMode under "Open with". It removes an MSI-installed NoteMode (0.2.x and
earlier) first, refuses to install over a newer version unless run with `/ALLOWDOWNGRADE`, and on
uninstall removes the file associations the app made for that user. Your notes and session in
`~/.notemode` are never touched.

`packaging/Test-Installer.ps1` smoke-tests it: silent install, files, shortcut, context menu,
upgrade over itself, uninstall (including the per-user associations), and an install without the
context menu task. It needs an elevated shell and changes the machine, so run it on a throwaway VM
(CI runs it on every tag and manual build).

## Microsoft Store package

`packaging/Build-Msix.ps1` produces the MSIX for Store submission (Native AOT, unsigned; Partner
Center signs it). It needs the identity values from Partner Center > Product identity, as parameters
or as the `STORE_PACKAGE_NAME`, `STORE_PUBLISHER` and `STORE_PUBLISHER_DISPLAY` environment
variables. For a local sideload test run it with `-Sign`, which creates a self-signed certificate
and prints the two commands to install it.

The packaged app declares its file types in `packaging/Package.appxmanifest` (Windows lists it under
"Open with" and Default apps) and gets a `notemode` command-line alias. It cannot write registry
associations, so Settings explains how to use Default apps instead. Logo images in
`packaging/Assets` are generated from the logo:

```bash
dotnet run --project tools/NoteMode.UiPreview -- --assets packaging/Assets
```

Listing copy and screenshots are in `docs/store/`; the privacy policy is `docs/privacy.md`.

## Marketing screenshots

```bash
dotnet run --project tools/NoteMode.UiPreview -- --store docs/store/screenshots
dotnet run --project tools/NoteMode.UiPreview -- --hero docs/store
```

Each screenshot is a 1920x1080 frame: a gradient, a headline and the real window rendered 1:1 from a
sample session. Headlines, colours and what each frame shows are in
`tools/NoteMode.UiPreview/StoreScreenshots.cs`. The editor font is the machine's monospace font
(Consolas on Windows), so render them on Windows for the published images.

## Releases

`.github/workflows/build.yml` builds every push and pull request to `main`. Pushing a `v*` tag then
builds the installer, runs the installer smoke test, and only if that passes publishes a GitHub
Release with:

- `NoteMode-v<ver>-win-x64.zip` (portable Native AOT build)
- `NoteMode-Setup-<ver>.exe`
- `SHA256SUMS.txt` (verify with `sha256sum -c`)

The unsigned MSIX for Partner Center is a workflow artifact, not part of the release.

The version comes from the tag (`v1.2.3` becomes assembly 1.2.3, shown in Settings > About, and
package 1.2.3.0). The `<Version>` in `NoteMode.csproj` is only the fallback for local builds; bump
it when tagging.

Manual runs (Actions > Build > Run workflow) build the installer and MSIX from any branch for
testing. They are never code-signed and are versioned `0.0.<run>`, which the installer refuses to
put over an installed release.

### Code signing

Only tag builds are signed (Azure Trusted Signing). Each job gets only the permissions it needs:
the signing job alone can request an OIDC token, and only the final release job can write to the
repository. Third-party actions (Azure login, Trusted Signing, the release upload) are pinned to
commit SHAs. Signing switches itself on once this is set up in the GitHub repository settings:

- **Environment `release`**: deployment limited to `v*` tags, with a required reviewer. The signing
  job runs in it, so every signed build waits for approval.
- **Environment secrets** (in `release`, not the repository): `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
  `AZURE_SUBSCRIPTION_ID`, for an app registration whose federated credential names this repo's
  `release` environment and which has the "Trusted Signing Certificate Profile Signer" role.
- **Variables**: `SIGNING_ENDPOINT`, `SIGNING_ACCOUNT` and `SIGNING_PROFILE` (set last; it is what
  turns signing on).

Without them every signing step is skipped and the outputs are unsigned. The Store identity
secrets (`STORE_PACKAGE_NAME`, `STORE_PUBLISHER`, `STORE_PUBLISHER_DISPLAY`) are needed for a
Store-ready MSIX; without them the MSIX gets a local test identity.
