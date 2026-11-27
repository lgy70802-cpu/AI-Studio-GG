#ifndef AppVersion
  #define AppVersion "0.2.14"
#endif
#define AppSource "..\artifacts\release\" + AppVersion + "\app"

[Setup]
AppId={{5DC279A8-9DAC-4660-B9B3-FF889E1088BB}
AppName=小屏宝PixBuddy
AppVersion={#AppVersion}
DefaultDirName={autopf}\小屏宝PixBuddy
DefaultGroupName=小屏宝PixBuddy
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UsePreviousPrivileges=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\release\{#AppVersion}
OutputBaseFilename=MiraBoxStudioNext-{#AppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\MiraBox.App\Assets\PixBuddy.ico
UninstallDisplayIcon={app}\MiraBox.App.exe
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: checkedonce

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml"
Source: "..\artifacts\packaging\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy
Source: "..\artifacts\packaging\VC_redist.x64.exe"; Flags: dontcopy

[InstallDelete]
Type: files; Name: "{autodesktop}\MiraBox Studio Next.lnk"
Type: files; Name: "{autoprograms}\MiraBox Studio Next\MiraBox Studio Next.lnk"
Type: files; Name: "{autoprograms}\MiraBox Studio Next\卸载 MiraBox Studio Next.lnk"

[Icons]
Name: "{group}\小屏宝PixBuddy"; Filename: "{app}\MiraBox.App.exe"; WorkingDir: "{app}"
Name: "{group}\卸载 小屏宝PixBuddy"; Filename: "{uninstallexe}"
Name: "{autodesktop}\小屏宝PixBuddy"; Filename: "{app}\MiraBox.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\MiraBox.App.exe"; Parameters: "{code:UpdateArguments}"; Flags: nowait runasoriginaluser; Check: IsUpdate
Filename: "{app}\MiraBox.App.exe"; Description: "启动 小屏宝PixBuddy"; Flags: nowait postinstall skipifsilent; Check: not IsUpdate

[Code]
function UpdateArguments(Param: String): String;
var Workspace: String;
begin
  Workspace := ExpandConstant('{param:WORKSPACE|}');
  Result := '';
  if Workspace <> '' then begin
    if Workspace[Length(Workspace)] = '\' then Workspace := Workspace + '\';
    Result := '--workspace "' + Workspace + '"';
  end;
end;

function IsUpdate: Boolean;
begin
  Result := ExpandConstant('{param:UPDATE|0}') = '1';
end;

function TargetDirectoryWritable: Boolean;
var Directory, Parent, Probe: String;
begin
  Result := False;
  Directory := WizardDirValue;
  while not DirExists(Directory) do begin
    Parent := ExtractFileDir(Directory);
    if (Parent = Directory) or (Parent = '') then exit;
    Directory := Parent;
  end;
  Probe := '';
  try
    Probe := GenerateUniqueName(Directory, '.tmp');
    Result := SaveStringToFile(Probe, 'MiraBox installation write check', False);
  except
    Result := False;
  end;
  if Probe <> '' then DeleteFile(Probe);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and not TargetDirectoryWritable then begin
    Result := False;
    SuppressibleMsgBox('当前安装模式无法写入此目录。请选择有写入权限的目录。' + #13#10 +
      '安装到 Program Files 等受保护目录时，请重新运行安装包并选择“为所有用户安装”，然后允许管理员授权。', mbError, MB_OK, IDOK);
  end;
end;

function HasWebView2: Boolean;
var Version: String;
begin
  Result := (RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0')) or
    (RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer; Installed: Cardinal;
begin
  Result := '';
  if not TargetDirectoryWritable then begin
    Result := '目标目录无法写入。请返回选择可写目录，或重新运行安装包并选择“为所有用户安装”以申请管理员授权。';
    exit;
  end;
  if not (RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) and (Installed = 1)) then begin
    ExtractTemporaryFile('VC_redist.x64.exe');
    if not ShellExec('runas', ExpandConstant('{tmp}\VC_redist.x64.exe'), '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
      Result := '录屏功能需要 Microsoft Visual C++ x64 运行库，请允许运行库安装后重试。';
      exit;
    end;
    if (ExitCode <> 0) and (ExitCode <> 3010) and (ExitCode <> 1638) then begin
      Result := 'Microsoft Visual C++ x64 运行库安装失败，请安装运行库后重试。';
      exit;
    end;
    if ExitCode = 3010 then NeedsRestart := True;
  end;
  if HasWebView2 then exit;
  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Result := '无法启动 WebView2 安装程序。请安装 Microsoft Edge WebView2 Runtime 后重试。'
  else if not HasWebView2 then
    Result := 'WebView2 安装尚未完成。请检查网络连接并安装 Microsoft Edge WebView2 Runtime 后重试。';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MiraBoxStudio.Next', Command) then
      if Pos(Lowercase(ExpandConstant('{app}\MiraBox.App.exe')), Lowercase(Command)) > 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MiraBoxStudio.Next');
end;
