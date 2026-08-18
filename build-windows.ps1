param(
    [string]$Version = "1.2.2",
    [string]$Architecture = "x64",
    [string]$InnoSetupCompiler = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$runtime = "win-$Architecture"
$output = Join-Path $root "dist\Windows10-11-$Architecture"
$publish = Join-Path $root "bin\publish\${runtime}-folder"
$stagingRoot = Join-Path $root "dist\.staging"
$packageBase = "PetFriends-v$Version-Windows10-11-$Architecture"
$portableBase = "$packageBase-Portable"
$portableRoot = Join-Path $stagingRoot $portableBase
$releaseInstaller = Join-Path $output "$packageBase.exe"
$releaseZip = Join-Path $output "$portableBase.zip"
$appExeName = "小欧公爵和小耶牧师桌宠.exe"

function Reset-BuildDirectory([string]$Path) {
    $rootPath = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a directory outside the repository: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

function Resolve-InnoSetupCompiler {
    if (-not [string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
        if (-not (Test-Path -LiteralPath $InnoSetupCompiler)) {
            throw "Inno Setup compiler was not found: $InnoSetupCompiler"
        }
        return (Resolve-Path -LiteralPath $InnoSetupCompiler).Path
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw "Inno Setup 6 is required to build the one-click installer."
}

Reset-BuildDirectory $output
Reset-BuildDirectory $publish
Reset-BuildDirectory $portableRoot

dotnet publish (Join-Path $root "PetFriends.csproj") `
    -c Release `
    -r $runtime `
    --self-contained true `
    --no-restore `
    --output $publish `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$requiredFiles = @(
    $appExeName,
    "UIAutomationProvider.dll",
    "WindowsBase.dll",
    "PresentationCore.dll",
    "PresentationFramework.dll",
    "coreclr.dll",
    "hostfxr.dll",
    "hostpolicy.dll"
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $requiredFile))) {
        throw "Required runtime file is missing from the folder-based package: $requiredFile"
    }
}

$readmeTemplate = Get-Content -Raw -Encoding UTF8 (Join-Path $root "README-Windows10-11.txt")
$readmeTemplate.Replace("{VERSION}", $Version) |
    Set-Content -LiteralPath (Join-Path $publish "README-Windows10-11.txt") -Encoding UTF8
Set-Content -LiteralPath (Join-Path $publish "VERSION.txt") -Value $Version -Encoding Ascii
Copy-Item -Path (Join-Path $publish "*") -Destination $portableRoot -Recurse -Force
Compress-Archive -LiteralPath $portableRoot -DestinationPath $releaseZip -CompressionLevel Optimal

$iscc = Resolve-InnoSetupCompiler
& $iscc `
    "/DMyAppVersion=$Version" `
    "/DSourceDir=$publish" `
    "/DSourceRoot=$root" `
    "/DOutputDir=$output" `
    "/DOutputBaseFilename=$packageBase" `
    (Join-Path $root "installer-windows.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $releaseInstaller)) {
    throw "Installer was not created: $releaseInstaller"
}
if (-not (Test-Path -LiteralPath $releaseZip)) {
    throw "Portable ZIP was not created: $releaseZip"
}

Write-Output "Installer: $releaseInstaller"
Write-Output "Portable archive: $releaseZip"
Write-Output "Runtime files: $((Get-ChildItem -LiteralPath $publish -Recurse -File).Count)"
