#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or -not [Environment]::Is64BitProcess) { throw 'Build requires 64-bit PowerShell 7 on Windows.' }
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'build'
New-Item -ItemType Directory -Path $out -Force | Out-Null
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$names = @('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Security.dll','System.Xml.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')
$refs = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
foreach ($name in $names) { $refs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile((Join-Path $framework $name))) }
$trees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
$files = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Recurse -Filter '*.cs') + @(Get-ChildItem -LiteralPath (Join-Path $root 'vendor\lunar-csharp') -Recurse -Filter '*.cs')
foreach ($file in $files) { $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($file.FullName), $null, $file.FullName)) }
$options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::WindowsApplication).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithPlatform([Microsoft.CodeAnalysis.Platform]::X64)
$comp = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('ChinaTime', $trees, $refs, $options)
$exe = Join-Path $out 'ChinaTime.exe'
$stream = [IO.File]::Create($exe)
$manifest = [IO.File]::OpenRead((Join-Path $root 'src\app.manifest'))
$icon = [IO.File]::OpenRead((Join-Path $root 'src\ChinaTime.ico'))
$resources = $comp.CreateDefaultWin32Resources($true, $false, $manifest, $icon)
try {
    $result = $comp.Emit($stream, $null, $null, $resources)
    foreach ($diag in $result.Diagnostics) { if ($diag.Severity -eq 'Error' -or $diag.Severity -eq 'Warning') { Write-Output $diag.ToString() } }
    if (-not $result.Success) { throw 'Compilation failed' }
} finally { $stream.Dispose(); $manifest.Dispose(); $icon.Dispose(); $resources.Dispose() }
Copy-Item -LiteralPath (Join-Path $root 'src\ChinaTime.exe.config') -Destination $out -Force
Get-FileHash -LiteralPath $exe -Algorithm SHA256 | Format-List
