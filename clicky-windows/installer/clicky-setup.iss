; clicky-setup.iss
; Inno Setup script for Clicky Windows installer

#define AppName "Clicky"
#define AppVersion "1.0"
#define AppPublisher "Farza"
#define AppURL "https://github.com/farza/clicky"
#define AppExeName "ClickyWindows.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputBaseFilename=ClickySetup
OutputDir=.\output
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Require Windows 11
MinVersion=10.0.22000

; Run as current user (no admin rights needed)
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start Clicky when Windows starts"; \
  GroupDescription: "Additional options:"; Flags: checked

[Files]
; Include all files from the publish output directory
Source: "..\ClickyWindows\bin\Release\net8.0-windows10.0.22000.0\win-x64\publish\*"; \
  DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

[Run]
; Launch after install
Filename: "{app}\{#AppExeName}"; Description: "Launch Clicky"; \
  Flags: nowait postinstall skipifsilent

[Registry]
; Register for startup if the user checked the startup task
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "Clicky"; \
  ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startup
