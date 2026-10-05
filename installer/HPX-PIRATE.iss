; ============================================================================
;  HPX PIRATE - Inno Setup script
;  Produces:  HPX-PIRATE-Setup.exe
;
;  Build steps:
;    1) dotnet publish src/HPXPirate/HPXPirate.csproj -c Release -r win-x64 ^
;         --self-contained false -o publish
;    2) iscc installer\HPX-PIRATE.iss
;
;  Requirements:
;    - Inno Setup 6.x  (https://jrsoftware.org/isdl.php)
;    - The "publish" folder must exist next to the repo root (created in step 1)
; ============================================================================

#define AppName        "HPX PIRATE"
#define AppShortName   "HPXPirate"
#define AppPublisher   "HPX Pirate"
#define AppVersion     "1.0.0"
#define AppExeName     "HPXPirate.exe"
#define AppId          "{{8F2A6C1E-9B4D-4E7A-A1C3-9E5D7B2F4C10}"
; Publish output folder (relative to this .iss file -> ..\publish)
#define PublishDir     "..\publish"
; .NET 8 Desktop Runtime (x64) required by the framework-dependent build
#define DotNetRuntimeUrl "https://dotnet.microsoft.com/en-us/download/dotnet/8.0"
#define DotNetInstaller  "windowsdesktop-runtime-8.0.10-win-x64.exe"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/groo65599-cyber/HEAVYy-PROJECT-X
AppSupportURL=https://github.com/groo65599-cyber/HEAVYy-PROJECT-X
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\dist
OutputBaseFilename=HPX-PIRATE-Setup
SetupIconFile=..\src\HPXPirate\Assets\hpx.ico
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; The application manifest requests administrator privileges, so the installer
; should request them as well (needed for restore points / system tuning).
PrivilegesRequired=admin
MinVersion=10.0
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "indonesian"; MessagesFile: "compiler:Languages\Indonesian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
; --- Main application (framework-dependent publish output) ---
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; --- Documentation ---
Source: "..\README.md";   DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "..\BUILD.md";    DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE";     DestDir: "{app}"; Flags: ignoreversion
; --- Optional bundled .NET runtime installer (place it in installer\redist if present) ---
Source: "redist\{#DotNetInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}";            Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";      Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: quicklaunchicon

[Run]
; Offer to install the .NET 8 Desktop Runtime if it is missing and we have a local copy
Filename: "{tmp}\{#DotNetInstaller}"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing .NET 8 Desktop Runtime..."; Flags: waituntilterminated skipifdoesntexist; Check: NeedsDotNetRuntime
; Launch the app after install
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; User data lives in %APPDATA%\HPX Pirate and is intentionally preserved.
; Only the install directory is removed.

[Code]
const
  DotNetDesktopRegKey = 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';

function NeedsDotNetRuntime(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  Ver: Int64;
begin
  // If a bundled runtime installer exists AND no WindowsDesktop.App 8.x is present,
  // we need to run the runtime installer.
  Result := True;
  if RegGetSubkeyNames(HKLM, DotNetDesktopRegKey, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if Pos('8.', Names[I]) = 1 then
      begin
        Result := False;
        Break;
      end;
    end;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;
