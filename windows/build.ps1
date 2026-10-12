$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$outputDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $frameworkDir 'csc.exe'
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationTypes.dll')
$compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+', '/utf8output',('/out:' + (Join-Path $outputDir 'Screenline.exe')),('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')))
$compilerArgs += '/win32icon:' + (Join-Path $projectRoot 'src\assets\Screenline.ico')
foreach ($reference in $refs) { $compilerArgs += '/reference:' + (Join-Path $frameworkDir $reference) }
foreach ($reference in @('System.Runtime.WindowsRuntime.dll','System.Numerics.dll','System.Numerics.Vectors.dll')) { $compilerArgs += '/reference:' + (Join-Path $frameworkDir $reference) }
$compilerArgs += '/reference:' + (Join-Path $env:WINDIR 'System32\WinMetadata\Windows.UI.winmd')
$compilerArgs += '/reference:' + (Join-Path $env:WINDIR 'System32\WinMetadata\Windows.Foundation.winmd')
$compilerArgs += '/reference:' + (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll')
$compilerArgs += Join-Path $projectRoot 'src\Screenline.cs'
$compilerArgs += Join-Path $projectRoot 'src\HangingVisuals.cs'
$compilerArgs += Join-Path $projectRoot 'src\ImageEditor.cs'
$compilerArgs += Join-Path $projectRoot 'src\DesktopMaterial.cs'
$compilerArgs += Join-Path $projectRoot 'src\RegionCapture.cs'
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Screenline compilation failed' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\Screenline.exe.config') -Destination (Join-Path $outputDir 'Screenline.exe.config') -Force
Write-Output (Join-Path $outputDir 'Screenline.exe')
