; Ravenne's Tools Menu — Installateur Inno Setup
; Compilation :
;   1) dotnet publish ../src/RavenneToolsMenu -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ../publish
;   2) ISCC.exe RavenneToolsMenu.iss
; Résultat : Output\RavenneToolsMenu-Setup.exe

#define AppName "Ravenne's Tools Menu"
#define AppVersion "1.0.0"
#define AppPublisher "Ravenne"
#define AppExeName "RavenneToolsMenu.exe"

[Setup]
AppId={{7C4DFF11-RAVE-NNE0-TOOL-5MENU0000001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\RavenneToolsMenu
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=Output
OutputBaseFilename=RavenneToolsMenu-Setup
SetupIconFile=..\src\RavenneToolsMenu\ravenne.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
; Le runtime .NET 9 Desktop est requis (téléchargé si absent)
; Fermer Ravenne's Tools Menu avant de remplacer ses fichiers (mises à jour)
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer une icône sur le &bureau"; GroupDescription: "Icônes:"
Name: "autostart"; Description: "Démarrer &automatiquement avec Windows"; GroupDescription: "Autre:"; Flags: checkedonce
Name: "launch"; Description: "Lancer {#AppName} maintenant"; GroupDescription: "Autre:"; Flags: checkedonce unchecked

[Files]
; L'exe publié en fichier unique (dossier ..\publish)
Source: "..\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\RavenneToolsMenu\ravenne.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Désinstaller {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Démarrage automatique (HKCU, aucune élévation requise)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "RavenneToolsMenu"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Lancer {#AppName}"; Flags: nowait postinstall skipifsilent; Tasks: launch

[UninstallRun]
; Fermer l'application avant désinstallation silencieuse
Filename: "{cmd}"; Parameters: "/C taskkill /im {#AppExeName} /f"; Flags: runhidden; RunOnceId: "KillApp"
