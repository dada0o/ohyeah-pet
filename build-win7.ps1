param(
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$stage = Join-Path $root "dist\Windows7-Legacy-v$Version"
$output = Join-Path $root "dist\PetFriends-v$Version-Windows7-Legacy-x86-x64.zip"
$build = Join-Path $root "bin\win7\Release\net48"

dotnet build (Join-Path $root "PetFriends.Win7.csproj") `
    -c Release `
    -p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null

$builtExecutables = @(Get-ChildItem -LiteralPath $build -Filter "*.exe" -File)
if ($builtExecutables.Count -ne 1) {
    throw "Expected exactly one Win7 executable, found $($builtExecutables.Count)"
}
$sourceExe = $builtExecutables[0]
$sourceConfig = Get-Item -LiteralPath "$($sourceExe.FullName).config"
$packagedExe = Join-Path $stage $sourceExe.Name
Copy-Item -LiteralPath $sourceExe.FullName -Destination $packagedExe
Copy-Item -LiteralPath $sourceConfig.FullName -Destination (Join-Path $stage $sourceConfig.Name)
Copy-Item -LiteralPath (Join-Path $root "README-Win7.md") -Destination (Join-Path $stage "README-Win7.txt")

$hash = (Get-FileHash -LiteralPath $packagedExe -Algorithm SHA256).Hash
"$hash  $($sourceExe.Name)" | Set-Content -LiteralPath (Join-Path $stage "SHA256SUMS.txt") -Encoding UTF8

$packageFiles = @(Get-ChildItem -LiteralPath $stage -File)
if ($packageFiles.Count -ne 4) {
    throw "Expected exactly four Win7 package files, found $($packageFiles.Count)"
}

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Force
}
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $output -CompressionLevel Optimal

Write-Output "Archive: $output"
