$ErrorActionPreference = 'Stop'
$distribution = Join-Path $PSScriptRoot 'dist'
foreach ($name in @('README.md','QUALITY.md','LICENSE','THIRD-PARTY-NOTICES.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $distribution $name) -Force
}
$version = [Version](Get-Item -LiteralPath (Join-Path $distribution 'Screenline.exe')).VersionInfo.FileVersion
$bundle = Join-Path (Split-Path -Parent $PSScriptRoot) ('Screenline-Windows-' + $version.ToString(3) + '.zip')
$files = @('Screenline.exe','Screenline.exe.config','README.md','QUALITY.md','LICENSE','THIRD-PARTY-NOTICES.txt') | ForEach-Object { Join-Path $distribution $_ }
Compress-Archive -LiteralPath $files -DestinationPath $bundle -Force
Get-Item -LiteralPath $bundle | Select-Object FullName,Length
Get-FileHash -LiteralPath (Join-Path $distribution 'Screenline.exe') -Algorithm SHA256 | Select-Object Path,Hash
