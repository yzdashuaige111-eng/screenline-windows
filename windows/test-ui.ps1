$ErrorActionPreference = 'Stop'
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')
$testExe = Join-Path $PSScriptRoot 'tests\UiSmoke.exe'
$arguments = @('/nologo','/target:exe','/platform:x64','/utf8output',('/out:'+$testExe),('/win32manifest:'+(Join-Path $PSScriptRoot 'src\app.manifest')))
foreach ($ref in $refs) { $arguments += '/reference:' + (Join-Path $frameworkDir $ref) }
$arguments += Join-Path $PSScriptRoot 'tests\UiSmoke.cs'
$arguments += Join-Path $PSScriptRoot 'tests\Fixtures.cs'
& (Join-Path $frameworkDir 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'UI test compilation failed' }
& $testExe (Join-Path $PSScriptRoot 'dist\Screenline.exe') (Join-Path $PSScriptRoot 'qa')
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath (Join-Path $PSScriptRoot 'qa\evidence\ui-tests.json'); throw 'UI smoke tests failed' }
Get-Content -LiteralPath (Join-Path $PSScriptRoot 'qa\evidence\ui-tests.json')
