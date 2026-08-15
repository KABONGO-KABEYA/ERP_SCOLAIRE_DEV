; Inno Setup 6 — optionnel.
; Prérequis : exécuter d'abord scripts\build-setup.ps1
; CI / local versionné :
;   ISCC.exe /DMyAppVersion=1.2.0 /DSetupSourceDir=<abs>\dist\setup /DInnoOutputDir=<abs>\dist\inno scripts\erp-scolaire-setup.iss
;
; Les /D* de la ligne de commande priment les #define locaux via #ifndef.

#define MyAppName "ERP Scolaire"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "ERP Administration Scolaire RDC"
#define MyAppExeName "ErpScolaire.Setup.exe"
#ifndef SetupSourceDir
  #define SetupSourceDir "..\dist\setup"
#endif
#ifndef InnoOutputDir
  #define InnoOutputDir "..\dist\inno"
#endif

[Setup]
AppId={{A7C3E91F-4B2D-4E8A-9F1C-ERP-SCOLAIRE-2026}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ERP Scolaire\Setup
DisableProgramGroupPage=yes
OutputDir={#InnoOutputDir}
OutputBaseFilename=DesktopSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallModes=x64compatible
WizardStyle=modern

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
; Tout le package généré par build-setup.ps1
Source: "{#SetupSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName} Installation"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName} Installation"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer l'assistant d'installation"; Flags: nowait postinstall skipifsilent
