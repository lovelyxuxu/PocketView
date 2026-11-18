$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$workDir = Join-Path $projectDir 'work'
$null = New-Item -ItemType Directory -Path $workDir -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /utf8output ('/out:' + (Join-Path $workDir 'StoreTests.exe')) /reference:System.Web.Extensions.dll (Join-Path $projectDir 'src\Store.cs') (Join-Path $PSScriptRoot 'StoreTests.cs')
if($LASTEXITCODE -ne 0){throw '测试编译失败'}
& (Join-Path $workDir 'StoreTests.exe') $workDir
if($LASTEXITCODE -ne 0){throw '数据测试失败'}
$report = Join-Path $workDir 'verification.txt'
if(Test-Path -LiteralPath $report){Move-Item -LiteralPath $report -Destination ($report + '.' + (Get-Date -Format 'yyyyMMddHHmmssfff') + '.previous')}
$process = Start-Process -FilePath (Join-Path $projectDir 'app\PocketView.exe') -ArgumentList '--verify' -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(60000)){throw '集成测试超时，请查看 work/verification-profile/浏览数据/运行.log'}
if(-not(Test-Path -LiteralPath $report)){throw '没有生成测试报告'}
Get-Content -LiteralPath $report
if(-not(Select-String -LiteralPath $report -Pattern '^SUCCESS ')){throw '集成测试失败'}
