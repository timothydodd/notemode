; NoteMode direct-download installer (Inno Setup 6). Built by packaging\Build-Installer.ps1, which
; passes the defines below.
;
;   /DAppVersion=1.2.3        required
;   /DStageDir=<dir>          required: the Native AOT publish folder
;   /DOutputDir=<dir>         required
;
; Installs per machine under Program Files\NoteMode (the same folder the old MSI used, so file
; associations made from Settings keep pointing at a valid exe), adds the Explorer context menu
; entries, and removes an MSI-installed NoteMode (0.2.x and earlier) first.
;
; Command line: /ALLOWDOWNGRADE installs over a newer version (refused otherwise).
; Tasks can be picked silently with /TASKS="contextmenu,desktopicon".

#ifndef AppVersion
  #error AppVersion is not defined; build with packaging\Build-Installer.ps1
#endif

#define AppId "{{D56F8563-E0D0-44E4-AC8E-0D7CE1726DEE}"
#define AppIdKey "{D56F8563-E0D0-44E4-AC8E-0D7CE1726DEE}_is1"
; UpgradeCode of the WiX MSI shipped up to 0.2.x.
#define LegacyMsiUpgradeCode "{388513B1-B436-40DC-BD15-6B52A352E7FA}"

[Setup]
AppId={#AppId}
AppName=NoteMode
AppVersion={#AppVersion}
AppVerName=NoteMode {#AppVersion}
AppPublisher=Tim Dodd
AppPublisherURL=https://github.com/timothydodd/notemode
AppSupportURL=https://github.com/timothydodd/notemode/issues
AppUpdatesURL=https://github.com/timothydodd/notemode/releases/latest
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\NoteMode
DefaultGroupName=NoteMode
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=NoteMode-Setup-{#AppVersion}
UninstallDisplayIcon={app}\NoteMode.exe
UninstallDisplayName=NoteMode
LicenseFile=license.rtf
; Machine-wide: Program Files, the context menu for every user, and the old MSI (a per-machine
; install) can only be removed elevated.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Tells Explorer to refresh icons and associations after install and uninstall.
ChangesAssociations=yes
; NoteMode caches every tab and restores its session, so closing it for an upgrade loses nothing.
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "contextmenu"; Description: "Add ""Edit with NoteMode"" to the Explorer context menu"; GroupDescription: "Explorer integration:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\NoteMode"; Filename: "{app}\NoteMode.exe"
Name: "{autodesktop}\NoteMode"; Filename: "{app}\NoteMode.exe"; Tasks: desktopicon

[Registry]
; Right-click any file > Edit with NoteMode.
Root: HKA; Subkey: "Software\Classes\*\shell\EditWithNoteMode"; ValueType: string; ValueName: ""; ValueData: "Edit with NoteMode"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\*\shell\EditWithNoteMode"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\NoteMode.exe"",0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\*\shell\EditWithNoteMode\command"; ValueType: string; ValueName: ""; ValueData: """{app}\NoteMode.exe"" ""%1"""; Tasks: contextmenu
; Right-click the background of a folder > Open NoteMode here.
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\EditWithNoteMode"; ValueType: string; ValueName: ""; ValueData: "Open NoteMode here"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\EditWithNoteMode"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\NoteMode.exe"",0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\EditWithNoteMode\command"; ValueType: string; ValueName: ""; ValueData: """{app}\NoteMode.exe"""; Tasks: contextmenu
; Lets "Open with" list NoteMode for any file without it taking over a file type.
Root: HKA; Subkey: "Software\Classes\Applications\NoteMode.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "NoteMode"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\NoteMode.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\NoteMode.exe"" ""%1"""

[Run]
Filename: "{app}\NoteMode.exe"; Description: "Start NoteMode"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
const
  UninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\';
  UserClassesKey = 'Software\Classes';
  ERROR_SUCCESS = 0;

function MsiEnumRelatedProducts(lpUpgradeCode: String; dwReserved: Cardinal; iProductIndex: Cardinal; lpProductBuf: String): Cardinal;
  external 'MsiEnumRelatedProductsW@msi.dll stdcall';

{ Compares dotted version strings numerically: -1, 0 or 1. }
function CompareVersions(A, B: String): Integer;
var
  PA, PB, NA, NB: Integer;
begin
  Result := 0;
  while (Result = 0) and ((A <> '') or (B <> '')) do
  begin
    PA := Pos('.', A);
    if PA = 0 then PA := Length(A) + 1;
    PB := Pos('.', B);
    if PB = 0 then PB := Length(B) + 1;
    NA := StrToIntDef(Copy(A, 1, PA - 1), 0);
    NB := StrToIntDef(Copy(B, 1, PB - 1), 0);
    Delete(A, 1, PA);
    Delete(B, 1, PB);
    if NA < NB then
      Result := -1
    else if NA > NB then
      Result := 1;
  end;
end;

function HasParam(const Name: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Name) = 0 then
      Result := True;
end;

function InitializeSetup: Boolean;
var
  Installed: String;
begin
  Result := True;
  { A test build (0.0.<run>) or an older release must not silently replace a newer one. }
  if RegQueryStringValue(HKLM, UninstallKey + '{#AppIdKey}', 'DisplayVersion', Installed)
    and (CompareVersions(Installed, '{#AppVersion}') > 0) and not HasParam('/ALLOWDOWNGRADE') then
  begin
    SuppressibleMsgBox('A newer version of NoteMode (' + Installed + ') is already installed. Run this installer with /ALLOWDOWNGRADE to replace it with {#AppVersion}.', mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

{ --- the old MSI -------------------------------------------------------------------------------- }

{ Product code of an installed NoteMode MSI, or '' when there is none. }
function LegacyMsiProductCode: String;
var
  Buffer: String;
begin
  Result := '';
  Buffer := StringOfChar(#0, 39);
  if MsiEnumRelatedProducts('{#LegacyMsiUpgradeCode}', 0, 0, Buffer) = ERROR_SUCCESS then
    Result := Copy(Buffer, 1, 38);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: String;
  ExitCode: Integer;
begin
  Result := '';
  Code := LegacyMsiProductCode;
  if Code = '' then
    Exit;
  Log('Removing the MSI-installed NoteMode ' + Code);
  if not Exec(ExpandConstant('{sys}\msiexec.exe'), '/x ' + Code + ' /qn /norestart', '', SW_HIDE, ewWaitUntilTerminated, ExitCode)
    or ((ExitCode <> 0) and (ExitCode <> 3010)) then
    Result := 'The previous NoteMode installation (MSI) could not be removed (msiexec exit code ' + IntToStr(ExitCode) + '). Uninstall it from Settings > Apps, then run this installer again.';
end;

{ --- uninstall ---------------------------------------------------------------------------------- }

{ Settings > File associations writes per-user ProgIds (NoteMode.<Category>) and points extensions
  at them. Remove them for the account running the uninstall, or those files would keep an icon and
  an "open" command for an exe that no longer exists. }
procedure RemoveUserFileAssociations;
var
  Names: TArrayOfString;
  Value: String;
  I: Integer;
begin
  if not RegGetSubkeyNames(HKCU, UserClassesKey, Names) then
    Exit;
  for I := 0 to GetArrayLength(Names) - 1 do
  begin
    if Copy(Names[I], 1, 1) = '.' then
    begin
      if RegQueryStringValue(HKCU, UserClassesKey + '\' + Names[I], '', Value) and (Pos('NoteMode.', Value) = 1) then
        RegDeleteKeyIncludingSubkeys(HKCU, UserClassesKey + '\' + Names[I]);
    end
    else if Pos('NoteMode.', Names[I]) = 1 then
      RegDeleteKeyIncludingSubkeys(HKCU, UserClassesKey + '\' + Names[I]);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveUserFileAssociations;
end;
