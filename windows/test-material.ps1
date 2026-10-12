param([switch]$GalleryOnly,[switch]$RenderOnly,[switch]$CompileOnly)
$ErrorActionPreference = 'Stop'
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $frameworkDir 'csc.exe'
$testExe = Join-Path $PSScriptRoot 'tests\MaterialSmoke.exe'
$arguments = @('/nologo','/target:exe','/platform:x64','/utf8output',('/out:'+$testExe),('/win32manifest:'+(Join-Path $PSScriptRoot 'src\app.manifest')))
foreach ($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll')) { $arguments += '/reference:' + (Join-Path $frameworkDir $name) }
$arguments += Join-Path $PSScriptRoot 'tests\MaterialSmoke.cs'
$arguments += Join-Path $PSScriptRoot 'tests\Fixtures.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Material test compilation failed' }
if ($CompileOnly) { return }
if ($RenderOnly) { & $testExe (Join-Path $PSScriptRoot 'dist\Screenline.exe') (Join-Path $PSScriptRoot 'qa-gallery') 'render' }
elseif ($GalleryOnly) { & $testExe (Join-Path $PSScriptRoot 'dist\Screenline.exe') (Join-Path $PSScriptRoot 'qa-gallery') 'gallery' }
else { & $testExe (Join-Path $PSScriptRoot 'dist\Screenline.exe') (Join-Path $PSScriptRoot 'qa-material') }
if ($LASTEXITCODE -ne 0) { throw 'Material test failed' }
