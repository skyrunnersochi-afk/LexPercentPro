#ifndef SourceDir
  #define SourceDir "..\artifacts\LexPercentPro-win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif
#ifndef AppVersion
  #define AppVersion "0.1.4"
#endif

[Setup]
AppId={{9D32CB35-A42B-4BC0-962F-B58E0D2E9A87}
AppName=LexPercent Pro
AppVersion={#AppVersion}
AppPublisher=LexPercent Pro
DefaultDirName={localappdata}\Programs\LexPercentPro\App
DefaultGroupName=LexPercent Pro
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=LexPercentPro-Setup-{#AppVersion}
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\LexPercentPro.exe
CloseApplications=no
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LexPercent Pro"; Filename: "{app}\LexPercentPro.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\LexPercent Pro"; Filename: "{app}\LexPercentPro.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\LexPercentPro.exe"; Description: "Запустить LexPercent Pro"; Flags: nowait postinstall skipifsilent
