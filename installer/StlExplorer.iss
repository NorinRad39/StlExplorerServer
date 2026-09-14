; ============================================================
;  Installateur Windows de STL Explorer (Inno Setup 7)
;
;  Ce script n'est pas lance directement : deploy\deploy-windows.ps1 publie
;  d'abord l'application, puis appelle ISCC.exe en passant les parametres
;  /DVersionApp et /DDossierSource.
;
;  Compilation manuelle equivalente :
;    "C:\Program Files\Inno Setup 7\ISCC.exe" installer\StlExplorer.iss ^
;        /DVersionApp=1.1.0 /DDossierSource=..\publish\windows
; ============================================================

#ifndef VersionApp
  #define VersionApp "1.0.0"
#endif

#ifndef DossierSource
  #define DossierSource "..\publish\windows"
#endif

#define NomApp "STL Explorer"
#define EditeurApp "NorinRad39"
#define ExeApp "StlExplorerClient.exe"

[Setup]
AppId={{8F3B1C42-7D5E-4A61-9C88-2E4F6B0A9D13}
AppName={#NomApp}
AppVersion={#VersionApp}
AppVerName={#NomApp} {#VersionApp}
AppPublisher={#EditeurApp}
VersionInfoVersion={#VersionApp}

DefaultDirName={autopf}\STLExplorer
DefaultGroupName={#NomApp}
DisableProgramGroupPage=yes

; Icone de l'assistant d'installation lui-meme. Les raccourcis (Bureau, menu
; Demarrer) et la fiche « Applications installees » prennent celle de l'executable,
; que MAUI genere a partir de Resources\AppIcon\appicon.png.
SetupIconFile={#SourcePath}\stlexplorer.ico
UninstallDisplayIcon={app}\{#ExeApp}

; Installation par utilisateur : pas d'elevation UAC, indispensable pour que
; la mise a jour automatique puisse lancer le setup sans invite administrateur.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

OutputDir={#SourcePath}\..\publish\installer
OutputBaseFilename=STLExplorerSetup-{#VersionApp}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; L'application est fermee par la mise a jour automatique avant de lancer le setup,
; mais on ferme aussi tout reste eventuel pour eviter les fichiers verrouilles.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Creer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"

[Files]
Source: "{#DossierSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#NomApp}"; Filename: "{app}\{#ExeApp}"
Name: "{autodesktop}\{#NomApp}"; Filename: "{app}\{#ExeApp}"; Tasks: desktopicon

[Run]
; Apres une mise a jour silencieuse (/SILENT), relancer l'application automatiquement.
Filename: "{app}\{#ExeApp}"; Description: "Lancer {#NomApp}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#ExeApp}"; Flags: nowait runasoriginaluser skipifnotsilent
