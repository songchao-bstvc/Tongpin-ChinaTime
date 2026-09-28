#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'build\ChinaTime.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run scripts/build.ps1 and scripts/test.ps1 first.' }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
$version = ($version.Split('.')[0..2] -join '.')
$dist = Join-Path $root 'dist'
$stage = Join-Path $root ('artifacts\package-' + [Guid]::NewGuid().ToString('N'))
$folder = Join-Path $stage ('Tongpin-ChinaTime-v' + $version)
New-Item -ItemType Directory -Path $folder -Force | Out-Null
New-Item -ItemType Directory -Path $dist -Force | Out-Null
foreach ($name in @('ChinaTime.exe', 'ChinaTime.exe.config')) {
    Copy-Item -LiteralPath (Join-Path $root ('build\' + $name)) -Destination $folder
}
foreach ($name in @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'licenses')) {
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination $folder -Recurse
}
Copy-Item -LiteralPath (Join-Path $root 'docs\QUICK_START.md') -Destination (Join-Path $folder '开始使用.md')
$zip = Join-Path $dist ('Tongpin-ChinaTime-v' + $version + '-windows-x64.zip')
Compress-Archive -LiteralPath $folder -DestinationPath $zip -CompressionLevel Optimal -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Value ($hash + '  ' + [IO.Path]::GetFileName($zip)) -Encoding utf8NoBOM
Get-Item -LiteralPath $zip | Select-Object Name, Length
Write-Output ('SHA256: ' + $hash)
