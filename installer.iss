#define MyAppName "AskCons"
#define MyAppVersion "0.1.2"
[Setup]
AppId={{7B61721B-6A3F-46E0-A0AE-1C724EF30C41}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName=AskCons V0.1.2
AppPublisher=AskCons
DefaultDirName={localappdata}\Programs\AskCons
DefaultGroupName=AskCons
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=installer-output
OutputBaseFilename=AskCons-Setup-0.1.2
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=AskCons.ico
UninstallDisplayIcon={app}\AskCons.exe
CloseApplications=yes
CloseApplicationsFilter=AskCons.exe
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\AskCons"; Filename: "{app}\AskCons.exe"
Name: "{autodesktop}\AskCons"; Filename: "{app}\AskCons.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\AskCons.exe"; Description: "Launch AskCons"; Flags: nowait postinstall skipifsilent
