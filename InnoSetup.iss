; Inno Setup Script for Ping Legacy
; Requires Inno Setup 6.0 or later: https://jrsoftware.org/isinfo.php

#define MyAppName "Ping Legacy"
#define MyAppVersion "2.0.6.0"
#define MyAppPublisher "Avnish Kumar"
#define MyAppURL "https://github.com/avikeid2007/Ping-Tool"
#define MyAppExeName "PingTool.WinUI3.exe"
#define MyAppId "{{7B8E3F4C-9D1A-4E2B-8F3C-1A2B3C4D5E6F}"

[Setup]
; Basic App Information
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE
; Output Configuration
OutputDir=Output\Installer
OutputBaseFilename=PingLegacy-{#MyAppVersion}-Setup
; SetupIconFile=PingTool.WinUI3\Assets\Logo.png
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; Version Information
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCopyright=Copyright (C) 2024-2026 {#MyAppPublisher}

; Architecture and Compatibility
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; Privileges
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; UI Configuration
DisableProgramGroupPage=yes
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Application Files (x64) - from publish output
Source: "Output\Publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: Is64BitInstallMode

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Check for .NET 8 Runtime
function IsDotNet8Installed: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function InitializeSetup: Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  
  // Check if .NET 8 Desktop Runtime is installed
  if not IsDotNet8Installed then
  begin
    if MsgBox('This application requires .NET 8 Desktop Runtime.' + #13#10 + 
              'Would you like to download it now?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 
                'https://dotnet.microsoft.com/download/dotnet/8.0', 
                '', '', SW_SHOW, ewNoWait, ResultCode);
      Result := False;
    end
    else
    begin
      Result := False;
    end;
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
