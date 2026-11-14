; Sona per-user Windows installer.
; Build with tools/installer/build-installer.ps1 so payload, engine pins and
; receipt values are generated, not typed by hand.

#ifndef MyAppVersion
  #define MyAppVersion "0.6.0"
#endif
#ifndef MySourceHead
  #define MySourceHead "0000000000000000000000000000000000000000"
#endif
#ifndef MyExeSHA256
  #define MyExeSHA256 "0000000000000000000000000000000000000000000000000000000000000000"
#endif
#ifndef MyRepoRoot
  #define MyRepoRoot "..\.."
#endif
#ifndef MySourceDir
  #define MySourceDir "..\..\artifacts\installer\payload"
#endif
#ifndef MyEngineSourceDir
  #define MyEngineSourceDir "..\..\artifacts\installer\payload-engine\whisper\Release"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "..\..\artifacts\installer"
#endif
#ifndef MyDefaultDir
  #define MyDefaultDir "{localappdata}\Programs\Sona\app"
#endif
#ifndef MyEngineTargetDir
  #define MyEngineTargetDir "{localappdata}\Sona\runtime\whisper\Release"
#endif

#ifdef Rehearsal
  #define MyAppName "Sona Installer Test"
  #define MyAppId "{{4D2B8E6A-1C3F-4A97-B5E0-7A9D2C6F1B84}}"
  #define MyOutputBaseFilename "Sona-Setup-Test"
#else
  #define MyAppName "Sona"
  #define MyAppId "{{9A3C6F1E-7B4D-4E2A-8C51-2F6D9B7A0E13}}"
  #define MyOutputBaseFilename "Sona-Setup-" + MyAppVersion
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=JhinxDev
AppPublisherURL=https://github.com/JhinxDev/Sona
AppSupportURL=https://github.com/JhinxDev/Sona/issues
AppUpdatesURL=https://github.com/JhinxDev/Sona/releases
DefaultDirName={#MyDefaultDir}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyOutputBaseFilename}
SetupIconFile={#MyRepoRoot}\src\Sona.App\Assets\Sona.ico
UninstallDisplayIcon={app}\Sona.exe
UninstallDisplayName={#MyAppName}
LicenseFile={#MyRepoRoot}\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AllowNoIcons=yes
DisableReadyPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "engine"; Description: "Install the pinned Whisper CPU speech engine (recommended)"; GroupDescription: "Local speech engine:"; Flags: checkedonce
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Self-contained app and CLI. No .NET installation is required.
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyRepoRoot}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyRepoRoot}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyRepoRoot}\SECURITY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyRepoRoot}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyRepoRoot}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion
; The app's fallback runtime root is %LOCALAPPDATA%\Sona\runtime. Model weights
; are deliberately not bundled; Sona downloads the selected model in-app.
Source: "{#MyEngineSourceDir}\*"; DestDir: "{#MyEngineTargetDir}"; Tasks: engine; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\Sona.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Sona.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Sona.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  receipt, exePath: String;
begin
  if CurStep = ssPostInstall then
  begin
    exePath := ExpandConstant('{app}\Sona.exe');
    StringChangeEx(exePath, '\', '\\', True);
    receipt :=
      '{' + #13#10 +
      '  "Version": "' + '{#MyAppVersion}' + '",' + #13#10 +
      '  "SourceHead": "' + '{#MySourceHead}' + '",' + #13#10 +
      '  "Path": "' + exePath + '",' + #13#10 +
      '  "SHA256": "' + '{#MyExeSHA256}' + '"' + #13#10 +
      '}' + #13#10;
    SaveStringToFile(ExpandConstant('{app}\installation.json'), receipt, False);
  end;
end;
