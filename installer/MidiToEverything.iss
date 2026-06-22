; Inno Setup script for MidiToEverything.
; Builds a per-user installer (no admin needed) for the self-contained Windows build.
;
; Compile from the repo root, after publishing the app, e.g.:
;   dotnet publish src/App/App.csproj -c Release -r win-x64 --self-contained true ^
;       -p:PublishSingleFile=true -p:Version=1.2.3 --output publish
;   iscc /DAppVersion=1.2.3 /DSourceExe="publish\MidiToEverything.exe" installer\MidiToEverything.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\src\App\bin\Release\net8.0-windows\win-x64\publish\MidiToEverything.exe"
#endif

#define AppName "MidiToEverything"
#define AppPublisher "guy7cc"
#define AppURL "https://github.com/guy7cc/MidiToEverything"
#define AppExeName "MidiToEverything.exe"

[Setup]
; A stable AppId so upgrades replace the previous install.
AppId={{6B9A3F2E-7C4D-4E1B-9A2C-3F5E8D1A7C20}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=.
OutputBaseFilename=MidiToEverything-Setup-{#AppVersion}
SetupIconFile=..\src\App\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
; Folder where users drop custom action plugins (docs/05 §10).
Name: "{app}\plugins"

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "..\samples\config.sample.json"; DestDir: "{app}"; Flags: ignoreversion
; Per-language UI translation files (docs/07). Users can edit or add languages here.
Source: "..\src\App\Resources\Localization\strings.*.json"; DestDir: "{app}\Resources\Localization"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
