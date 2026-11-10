; ============================================================================
;  OnTop — Inno Setup installer script (Inno Setup 6.2+)
;  Roadmap: "Installer / Uninstaller (owner request — high priority)"
;
;  Build steps are in installer\BUILD.md. Short version:
;    1) dotnet publish ..\OnTop\OnTop.csproj -c Release -r win-x64 --self-contained false -o publish
;    2) Open this file in Inno Setup Compiler → Compile
;    → Output\OnTopSetup-<version>.exe
;
;  SECURITY notes (SECURITY.md applies to the installer too):
;    • PrivilegesRequired=lowest — per-user install by default, no elevation
;      needed (matches the app's asInvoker manifest). The wizard still offers
;      an all-users install, which triggers a normal UAC prompt.
;    • The ONLY network access is the optional .NET 8 Desktop Runtime download,
;      from Microsoft's fixed HTTPS aka.ms redirect, and only when the runtime
;      is missing. Nothing else is downloaded; nothing is transmitted.
;    • Uninstall asks before touching %APPDATA%\OnTop (user data is sacred).
; ============================================================================

#define MyAppName "OnTop"
#define MyAppVersion "0.43.0"
#define MyAppPublisher "Godswrath88"
#define MyAppExeName "OnTop.exe"
; The publish folder produced by `dotnet publish` (relative to this .iss):
#define PublishDir "publish"

[Setup]
AppId={{6F2A9C41-8B7D-4E5A-9C13-0NTOP0250001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputBaseFilename=OnTopSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Per-user by default (no UAC), but the user may choose all-users in the wizard.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; 64-bit app
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Roadmap: Desktop shortcut (checkbox), Start Menu shortcut (checkbox),
; optional launch-at-startup (Startup-folder shortcut — the same mechanism the
; app itself manages, so the app's own toggle stays in sync with it).
Name: "desktopicon";  Description: "Create a &Desktop shortcut";            GroupDescription: "Shortcuts:"
Name: "startmenu";    Description: "Create a Start &Menu shortcut";         GroupDescription: "Shortcuts:"
Name: "startupentry"; Description: "&Launch OnTop when Windows starts";     GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenu
Name: "{autodesktop}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"; Tasks: startupentry

[Run]
; Roadmap: "Run OnTop now" on finish.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// ───────────────────────── .NET 8 Desktop Runtime bootstrap ─────────────────
// Roadmap: "detect and install prerequisites automatically". Detection runs
// `dotnet --list-runtimes` and looks for Microsoft.WindowsDesktop.App 8.x.
// If absent, the installer offers to download Microsoft's runtime installer
// over HTTPS (fixed aka.ms link) and run it silently. If the download can't
// happen (offline), a clear message tells the user what to install.

const
  DotNetDesktopRuntimeUrl =
    'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';

var
  NeedsDotNet: Boolean;
  DownloadPage: TDownloadWizardPage;

function HasDotNetDesktop8: Boolean;
var
  ResultCode: Integer;
  TmpFile, Output: AnsiString;
  TmpPath: String;
begin
  Result := False;
  TmpPath := ExpandConstant('{tmp}\dotnet_runtimes.txt');
  // Run via cmd so we can redirect output to a file we can read back.
  if Exec(ExpandConstant('{cmd}'),
          '/C dotnet --list-runtimes > "' + TmpPath + '" 2>&1',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringFromFile(TmpPath, Output) then
      Result := Pos('Microsoft.WindowsDesktop.App 8.', String(Output)) > 0;
  end;
  DeleteFile(TmpPath);
end;

procedure InitializeWizard;
begin
  NeedsDotNet := not HasDotNetDesktop8;
  if NeedsDotNet then
    DownloadPage := CreateDownloadPage(
      'Prerequisite: .NET 8 Desktop Runtime',
      'OnTop needs the .NET 8 Desktop Runtime, which was not found. ' +
      'Setup will download it from Microsoft and install it silently.',
      nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if NeedsDotNet and (CurPageID = wpReady) then
  begin
    DownloadPage.Clear;
    DownloadPage.Add(DotNetDesktopRuntimeUrl, 'windowsdesktop-runtime.exe', '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
        if not Exec(ExpandConstant('{tmp}\windowsdesktop-runtime.exe'),
                    '/install /quiet /norestart', '', SW_SHOW,
                    ewWaitUntilTerminated, ResultCode) then
        begin
          MsgBox('The .NET runtime installer could not be started. ' +
                 'Please install the ".NET 8 Desktop Runtime (x64)" from ' +
                 'https://dotnet.microsoft.com/download and run this setup again.',
                 mbError, MB_OK);
          Result := False;
        end;
      except
        MsgBox('The .NET 8 Desktop Runtime could not be downloaded (offline?). ' +
               'Please install the ".NET 8 Desktop Runtime (x64)" from ' +
               'https://dotnet.microsoft.com/download and run this setup again.',
               mbError, MB_OK);
        Result := False;
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

// ───────────────────────── Uninstall: user data is sacred ───────────────────
// Ask before removing %APPDATA%\OnTop (settings, zones, profiles, stats, logs).
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ConfigRoot: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    ConfigRoot := ExpandConstant('{userappdata}\OnTop');
    // SAFETY GUARD: DelTree is destructive — refuse to run unless the resolved
    // path really ends in \OnTop under the user profile. Belt-and-suspenders
    // against any future edit or constant-expansion surprise.
    if (Length(ConfigRoot) < 10) or
       (Copy(ConfigRoot, Length(ConfigRoot) - 5, 6) <> '\OnTop') then
      Exit;
    if DirExists(ConfigRoot) then
      if MsgBox('Also delete your OnTop settings, zones, profiles, statistics, ' +
                'and logs?' + #13#10 + '(' + ConfigRoot + ')' + #13#10#13#10 +
                'Choose No to keep them for a future reinstall.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(ConfigRoot, True, True, True);
  end;
end;
