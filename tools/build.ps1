$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $projectDir 'app'
& (Join-Path $PSScriptRoot 'restore-sdk.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectDir 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/utf8output', ('/out:' + (Join-Path $appDir 'PocketView.exe')), '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Web.Extensions.dll', ('/reference:' + (Join-Path $appDir 'Microsoft.Web.WebView2.Core.dll')), ('/reference:' + (Join-Path $appDir 'Microsoft.Web.WebView2.WinForms.dll')))
$iconPath = Join-Path $appDir 'assets\pocketview.ico'
if(Test-Path -LiteralPath $iconPath){$arguments += '/win32icon:' + $iconPath}
& $compiler @arguments @sourceFiles
if($LASTEXITCODE -ne 0){throw '编译失败'}
Write-Output ('已生成 ' + (Join-Path $appDir 'PocketView.exe'))
