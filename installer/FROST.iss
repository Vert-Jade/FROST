#define MyAppName "FROST"
#define MyAppPublisher "Vert-Jade"
#define MyAppExeName "FROST.exe"
#define MyAppVersion GetStringFileInfo(AddBackslash(SourcePath) + "..\\release\\publish\\win-x64\\FROST.exe", "ProductVersion")

#ifndef PublishDir
  #define PublishDir AddBackslash(SourcePath) + "..\\release\\publish\\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir AddBackslash(SourcePath) + "..\\release\\installer"
#endif

[Setup]
AppId={{AE6E2D75-8F86-4F89-8D63-781907EFC1E8}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\FROST
DefaultGroupName=FROST
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=FROST_v{#MyAppVersion}_setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\Ressources\Icones\frost.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
Name: "{app}\Videos"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FROST"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\FROST"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer FROST"; Flags: nowait postinstall skipifsilent
