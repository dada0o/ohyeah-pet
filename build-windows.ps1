param(
    [string]$Version = "1.2.0",
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$runtime = "win-$Architecture"
$output = Join-Path $root "dist\Windows10-11-$Architecture"
$publish = Join-Path $root "bin\publish\$runtime"
$packageBase = "PetFriends-v$Version-Windows10-11-$Architecture"
$releaseExe = Join-Path $output "$packageBase.exe"
$releaseZip = Join-Path $output "$packageBase.zip"

New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($staleFile in @($releaseExe, $releaseZip)) {
    if (Test-Path -LiteralPath $staleFile) {
        Remove-Item -LiteralPath $staleFile -Force
    }
}

dotnet publish (Join-Path $root "PetFriends.csproj") `
    -c Release `
    -r $runtime `
    --self-contained true `
    --no-restore `
    --output $publish `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$publishedExecutables = @(Get-ChildItem -LiteralPath $publish -Filter "*.exe" -File)
if ($publishedExecutables.Count -ne 1) {
    throw "Expected exactly one published executable, found $($publishedExecutables.Count)"
}
$sourceExe = $publishedExecutables[0].FullName
Copy-Item -LiteralPath $sourceExe -Destination $releaseExe
Compress-Archive -LiteralPath $releaseExe -DestinationPath $releaseZip -CompressionLevel Optimal

Write-Output "Executable: $releaseExe"
Write-Output "Archive: $releaseZip"
