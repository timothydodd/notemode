<#
.SYNOPSIS
  Builds the direct-download installer, NoteMode-Setup-<ver>.exe.

.DESCRIPTION
  Publishes NoteMode as Native AOT win-x64 (needs the Visual Studio C++ build tools) into
  artifacts\installer-stage and compiles packaging\installer\NoteMode.iss with Inno Setup 6.

.PARAMETER Stage
  Publish  only publish the binaries into artifacts\installer-stage
  Compile  only compile the installer from an existing stage folder
  All      both (default)
  CI runs the two halves separately so the binaries can be code-signed in between; the installer
  itself is signed after Compile.
.PARAMETER Version
  Three-part version, the same one the app is built with.

.EXAMPLE
  .\packaging\Build-Installer.ps1 -Version 0.3.0
#>
[CmdletBinding()]
param(
    [ValidateSet("All", "Publish", "Compile")]
    [string]$Stage = "All",
    [string]$Version = "0.4.0",
    [string]$Output = "artifacts"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$stageDir = Join-Path $root "artifacts\installer-stage"
$outDir = Join-Path $root $Output
New-Item $outDir -ItemType Directory -Force | Out-Null

if ($Stage -ne "Compile") {
    if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
    Write-Host "Publishing NoteMode (Native AOT win-x64)..."
    dotnet publish (Join-Path $root "src\NoteMode") -c Release -r win-x64 -o $stageDir -p:Version=$Version
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
    Get-ChildItem $stageDir -Recurse -Filter *.pdb | Remove-Item
    if ($Stage -eq "Publish") { return }
}
if (-not (Test-Path (Join-Path $stageDir "NoteMode.exe"))) {
    throw "Nothing staged at $stageDir. Run with -Stage Publish (or All) first."
}

$iscc = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it from https://jrsoftware.org/isdl.php or 'choco install innosetup'." }

Write-Host "Compiling NoteMode-Setup-$Version.exe..."
& $iscc /Qp "/DAppVersion=$Version" "/DStageDir=$stageDir" "/DOutputDir=$outDir" (Join-Path $PSScriptRoot "installer\NoteMode.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

Get-ChildItem $outDir -Filter "NoteMode-Setup-$Version.exe" | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name
} | Tee-Object -FilePath (Join-Path $outDir "SHA256SUMS.txt")
