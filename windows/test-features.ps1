param([switch]$CompileOnly)
$ErrorActionPreference = 'Stop'
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$testExe = Join-Path $PSScriptRoot 'tests\FeatureSmoke.exe'
$arguments = @('/nologo','/target:exe','/platform:x64','/utf8output',('/out:'+$testExe),('/win32manifest:'+(Join-Path $PSScriptRoot 'src\app.manifest')))
foreach ($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','WPF\WindowsBase.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')) { $arguments += '/reference:' + (Join-Path $frameworkDir $name) }
$arguments += Join-Path $PSScriptRoot 'tests\FeatureSmoke.cs'
$arguments += Join-Path $PSScriptRoot 'tests\Fixtures.cs'
& (Join-Path $frameworkDir 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Feature test compilation failed' }
if ($CompileOnly) { return }
& $testExe (Join-Path $PSScriptRoot 'dist\Screenline.exe') (Join-Path $PSScriptRoot 'qa-features')
Get-Content -LiteralPath (Join-Path $PSScriptRoot 'qa-features\evidence\feature-tests.json')
if ($LASTEXITCODE -ne 0) { throw 'Native feature checks failed' }
