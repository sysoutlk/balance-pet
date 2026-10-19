# Builds balance-pet.exe with the in-box C# compiler: no SDK, no NuGet, no network.
#
#   .\build.ps1            normal build
#   .\build.ps1 -SkipIcon  skip embedding assets\app.ico
param(
    [switch]$SkipIcon
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root 'src'
$icon = Join-Path $root 'assets\app.ico'
$exe = Join-Path $root 'balance-pet.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }
$framework = Split-Path $csc
New-Item -ItemType Directory -Force -Path (Join-Path $root 'assets') | Out-Null

$references = @(
    'mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Drawing.dll',
    'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'System.Configuration.dll',
    'System.Net.Http.dll', 'System.Xaml.dll', 'WindowsBase.dll',
    'PresentationCore.dll', 'PresentationFramework.dll', 'System.Security.dll', 'System.Xml.dll'
)

function Resolve-FrameworkAssembly([string]$name) {
    $direct = Join-Path $framework $name
    if (Test-Path $direct) { return $direct }
    # WPF assemblies (WindowsBase, PresentationCore, PresentationFramework) live in the GAC.
    $roots = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL'),
        (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_64'),
        (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_32')
    )
    foreach ($gacRoot in $roots) {
        $versioned = Join-Path $gacRoot ([System.IO.Path]::GetFileNameWithoutExtension($name))
        if (-not (Test-Path $versioned)) { continue }
        $folder = Get-ChildItem -Path $versioned -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($folder) {
            $file = Join-Path $folder.FullName $name
            if (Test-Path $file) { return $file }
        }
    }
    throw "cannot resolve framework assembly: $name"
}

function Write-ResponseFile([string]$path, [bool]$withIcon) {
    $lines = @(
        '/noconfig',
        '/nostdlib',
        '/langversion:5',
        '/platform:anycpu',
        '/target:winexe',
        '/optimize+',
        '/warn:4',
        '/codepage:65001'
    )
    foreach ($reference in $references) {
        $file = Resolve-FrameworkAssembly $reference
        $lines += ('/r:"' + $file + '"')
    }
    if ($withIcon) { $lines += ('/win32icon:"' + $icon + '"') }
    $lines += ('/win32manifest:"' + (Join-Path $srcDir 'app.manifest') + '"')
    $lines += ('/out:"' + $exe + '"')
    Get-ChildItem $srcDir -Filter *.cs | Where-Object { $_.Name -ne 'Setup.cs' } | Sort-Object Name | ForEach-Object {
        $lines += ('"' + $_.FullName + '"')
    }
    Set-Content -Path $path -Value $lines -Encoding ASCII
}

$response = Join-Path $env:TEMP 'balance-pet-csc.rsp'
$hasIcon = (Test-Path $icon) -and (-not $SkipIcon)
Write-ResponseFile $response $hasIcon
& $csc "@$response"
if ($LASTEXITCODE -ne 0) { throw "compile failed with exit code $LASTEXITCODE" }

# First build has no icon yet: render the mascot, then rebuild once with it embedded.
if ((-not (Test-Path $icon)) -and (-not $SkipIcon)) {
    & $exe --make-icon $icon | Out-Null
    Write-ResponseFile $response $true
    & $csc "@$response"
    if ($LASTEXITCODE -ne 0) { throw "compile failed with exit code $LASTEXITCODE" }
}
Remove-Item $response -ErrorAction SilentlyContinue
Write-Host "built: $exe"

# Installer: embeds balance-pet.exe so the user can choose an install folder.
$setupExe = Join-Path $root 'balance-pet-setup.exe'
$setupRsp = Join-Path $env:TEMP 'balance-pet-setup.rsp'
$setupLines = @(
    '/noconfig',
    '/nostdlib',
    '/langversion:5',
    '/platform:anycpu',
    '/target:winexe',
    '/optimize+',
    '/warn:4',
    '/codepage:65001'
)
foreach ($reference in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll')) {
    $file = Resolve-FrameworkAssembly $reference
    $setupLines += ('/r:"' + $file + '"')
}
if ((Test-Path $icon) -and (-not $SkipIcon)) { $setupLines += ('/win32icon:"' + $icon + '"') }
$setupLines += ('/resource:"' + $exe + '",balance-pet.exe')
$setupLines += ('/out:"' + $setupExe + '"')
$setupLines += ('"' + (Join-Path $srcDir 'Setup.cs') + '"')
Set-Content -Path $setupRsp -Value $setupLines -Encoding ASCII
& $csc "@$setupRsp"
if ($LASTEXITCODE -ne 0) { throw "setup compile failed with exit code $LASTEXITCODE" }
Remove-Item $setupRsp -ErrorAction SilentlyContinue
Write-Host "built: $setupExe"
