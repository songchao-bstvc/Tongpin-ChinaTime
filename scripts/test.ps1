#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'build\ChinaTime.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run scripts/build.ps1 first.' }
$testDir = Join-Path $root ('artifacts\self-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
$process = Start-Process -FilePath $exe -ArgumentList @('--self-test', '--data-dir', ('"' + $testDir + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Self-test failed: exit $($process.ExitCode). See $testDir" }
$results = Get-Content -LiteralPath (Join-Path $testDir 'self-test.json') -Raw | ConvertFrom-Json
if ($results.Count -ne 24 -or @($results | Where-Object { -not $_.passed }).Count -gt 0) { throw 'Expected 24 passing checks.' }
New-Item -ItemType Directory -Path (Join-Path $root 'artifacts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $testDir 'self-test.json') -Destination (Join-Path $root 'artifacts\self-test.json') -Force
$results | Select-Object name, passed | Format-Table -AutoSize
Write-Output 'PASS: 24 / 24. Tests use an isolated directory; personal data is untouched.'
