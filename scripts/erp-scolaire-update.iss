; Inno Setup 6 — package de MISE À JOUR ERP Scolaire (installation existante).
; Prérequis : .\scripts\build-update.ps1 -TryInnoSetup

#define MyAppName "ERP Scolaire"
#ifndef MyAppVersion
  #define MyAppVersion "2026.10.5"
#endif
#define MyAppPublisher "ERP Administration Scolaire RDC"
#define MyAppExeName "ErpScolaire.Update.exe"
#ifndef UpdateSourceDir
  #define UpdateSourceDir "..\dist\update"
#endif
#ifndef InnoOutputDir
  #define InnoOutputDir "..\dist\inno"
#endif

[Setup]
AppId={{B8D4F92E-5C3E-4A9B-8E2D-ERP-UPDATE-202609}
AppName={#MyAppName} — Mise à jour
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ERP Scolaire\Update
DisableProgramGroupPage=yes
OutputDir={#InnoOutputDir}
OutputBaseFilename=ERP_Scolaire_Update_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
Source: "{#UpdateSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\ErpScolaire.Setup.exe"

[Icons]
Name: "{autoprograms}\{#MyAppName} Mise à jour"; Filename: "{app}\{#MyAppExeName}"

[Run]
; L'assistant exige requireAdministrator : conserver le jeton élevé de Setup.
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer la mise à jour ERP Scolaire"; Flags: nowait postinstall skipifsilent runascurrentuser
