; Inno Setup 6 — optionnel.
; Prérequis : exécuter d'abord scripts\build-setup.ps1
; Puis : ISCC.exe scripts\erp-scolaire-setup.iss

#define MyAppName "ERP Scolaire"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ERP Administration Scolaire RDC"
#define MyAppExeName "ErpScolaire.Setup.exe"

[Setup]
AppId={{A7C3E91F-4B2D-4E8A-9F1C-ERP-SCOLAIRE-2026}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ERP Scolaire\Setup
DisableProgramGroupPage=yes
OutputDir=..\dist\inno
OutputBaseFilename=ERP-Scolaire-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallModes=x64compatible
WizardStyle=modern

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
; Tout le package généré par build-setup.ps1
Source: "..\dist\setup\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName} Installation"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName} Installation"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer l'assistant d'installation"; Flags: nowait postinstall skipifsilent
