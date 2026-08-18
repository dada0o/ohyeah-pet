#ifndef MyAppVersion
  #define MyAppVersion "1.2.2"
#endif
#ifndef SourceDir
  #error SourceDir must be supplied by build-windows.ps1
#endif
#ifndef SourceRoot
  #error SourceRoot must be supplied by build-windows.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by build-windows.ps1
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "PetFriends-Windows10-11-x64"
#endif

#define MyAppName "小欧公爵和小耶牧师桌宠"
#define MyAppExeName "小欧公爵和小耶牧师桌宠.exe"

[Setup]
AppId={{D5AF791B-5134-48FC-936C-28362D82F78B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=dada0o
AppPublisherURL=https://github.com/dada0o/ohyeah-pet
AppSupportURL=https://github.com/dada0o/ohyeah-pet/issues
DefaultDirName={localappdata}\Programs\PetFriends
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile={#SourceRoot}\Assets\pet.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} Windows 10/11 安装程序

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项："; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动{#MyAppName}"; Flags: nowait postinstall skipifsilent
