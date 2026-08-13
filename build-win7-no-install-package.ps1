param(
    [string]$DotnetPath = "dotnet",
    [string]$NuGetPackages = "",
    [string]$OutputDirectory = "publish\PetFriends-v1.2.0-Windows7-NoInstall-x86-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $repoRoot "obj\dotnet-cli-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
if ($NuGetPackages) {
    $env:NUGET_PACKAGES = [System.IO.Path]::GetFullPath($NuGetPackages)
}

$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$publishRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "publish"))
if (-not $outputRoot.StartsWith($publishRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be inside the repository publish directory."
}

$projectPath = Join-Path $repoRoot "PetFriends.Win7NoInstall.csproj"
& $DotnetPath restore $projectPath --ignore-failed-sources
if ($LASTEXITCODE -ne 0) {
    throw "The Windows 7 package restore failed."
}
& $DotnetPath build $projectPath -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "The Windows 7 no-install build failed."
}

$appSource = Join-Path $repoRoot "bin\win7-no-install\Release\net35"
$app = Get-ChildItem -LiteralPath $appSource -File -Filter "*.exe" | Select-Object -First 1
if (-not $app) {
    throw "The Windows 7 executable was not found."
}
$configPath = "$($app.FullName).config"
$zipPath = "$outputRoot.zip"

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

Copy-Item -LiteralPath $app.FullName -Destination $outputRoot
Copy-Item -LiteralPath $configPath -Destination $outputRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "README-Win7-NoInstall.txt") -Destination $outputRoot

$hashFiles = Get-ChildItem -LiteralPath $outputRoot -File |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    Sort-Object Name
$hashLines = foreach ($file in $hashFiles) {
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    "$hash  $($file.Name)"
}
[System.IO.File]::WriteAllLines(
    (Join-Path $outputRoot "SHA256SUMS.txt"),
    $hashLines,
    (New-Object System.Text.UTF8Encoding($false))
)

Compress-Archive -LiteralPath $outputRoot -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Write-Host "Package: $zipPath"
Write-Host "SHA256: $zipHash"
