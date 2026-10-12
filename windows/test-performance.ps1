param([string]$Binary = '', [string]$Output = '')
$ErrorActionPreference = 'Stop'
if (!$Binary) { $Binary = Join-Path $PSScriptRoot 'dist/Screenline.exe' }
if (!$Output) { $Output = Join-Path $PSScriptRoot 'qa-performance/current' }
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$runtime = Join-Path $PSScriptRoot 'qa-performance/runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$testExe = Join-Path $runtime 'PerformanceProbe.exe'
$arguments = @('/nologo','/target:exe','/platform:x64','/utf8output',('/out:'+$testExe))
foreach ($name in @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Xaml.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll')) { $arguments += '/reference:' + (Join-Path $frameworkDir $name) }
$arguments += Join-Path $PSScriptRoot 'tests/PerformanceProbe.cs'
& (Join-Path $frameworkDir 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Performance probe compilation failed' }
& $testExe (Resolve-Path -LiteralPath $Binary).Path $Output
if ($LASTEXITCODE -ne 0) { throw 'Performance probe failed' }
