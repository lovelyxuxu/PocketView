$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $projectDir 'app'
$workDir = Join-Path $projectDir 'work'
$sdkVersion = '1.0.3179.45'
$sdkDir = Join-Path $workDir ('webview2-sdk-' + $sdkVersion)
$packagePath = Join-Path $workDir ('webview2-sdk-' + $sdkVersion + '.zip')
$requiredFiles = @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')
if(@($requiredFiles | Where-Object { -not(Test-Path -LiteralPath (Join-Path $appDir $_)) }).Count -eq 0){return}
$null = New-Item -ItemType Directory -Path $workDir -Force
$null = New-Item -ItemType Directory -Path $appDir -Force
$packageUrl = 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/' + $sdkVersion + '/microsoft.web.webview2.' + $sdkVersion + '.nupkg'
if(-not(Test-Path -LiteralPath $packagePath)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $packageUrl -OutFile ($packagePath + '.tmp') -UseBasicParsing
    Move-Item -LiteralPath ($packagePath + '.tmp') -Destination $packagePath
}
Expand-Archive -LiteralPath $packagePath -DestinationPath $sdkDir -Force
$sources = @('lib\net462\Microsoft.Web.WebView2.Core.dll','lib\net462\Microsoft.Web.WebView2.WinForms.dll','runtimes\win-x64\native\WebView2Loader.dll')
for($index=0;$index -lt $requiredFiles.Count;$index++) {
    Copy-Item -LiteralPath (Join-Path $sdkDir $sources[$index]) -Destination (Join-Path $appDir $requiredFiles[$index]) -Force
}
Write-Output ('已恢复 WebView2 SDK ' + $sdkVersion)
