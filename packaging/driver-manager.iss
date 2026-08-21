#define MyAppName "Driver Manager"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Shoropio Corporation"
#define MyAppExeName "DriverManager.App.exe"

[Setup]
AppId={{8A2C6D3B-1F4E-4B7A-9C5D-2E8F0A3B6D41}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\DriverManager
DefaultGroupName={#MyAppName}
SetupIconFile=..\DriverManager.App\Assets\app.ico
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=..\output
OutputBaseFilename=DriverManager-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64os

#ifdef MySign
SignTool=signtool
SignedUninstaller=yes
#endif

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "..\DriverManager.App\bin\Release\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Ejecutar {#MyAppName}"; Flags: nowait postinstall skipifsilent shellexec
