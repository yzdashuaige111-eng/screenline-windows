$ErrorActionPreference = 'Stop'
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$runtime = Join-Path $PSScriptRoot 'qa-design\runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dist\Screenline.exe') -Destination (Join-Path $runtime 'Screenline.exe') -Force
$testExe = Join-Path $runtime 'DesignPreview.exe'
$arguments = @('/nologo','/target:exe','/platform:x64','/utf8output',('/out:'+$testExe),('/reference:'+(Join-Path $PSScriptRoot 'dist\Screenline.exe')))
foreach ($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')) { $arguments += '/reference:' + (Join-Path $frameworkDir $name) }
$arguments += Join-Path $PSScriptRoot 'tests\DesignPreview.cs'
$arguments += Join-Path $PSScriptRoot 'tests\Fixtures.cs'
& (Join-Path $frameworkDir 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Design preview compilation failed' }
& $testExe (Join-Path $PSScriptRoot 'qa-design\evidence')
if ($LASTEXITCODE -ne 0) { throw 'Design preview rendering failed' }
