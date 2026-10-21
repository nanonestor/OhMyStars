param(
    [string]$GameDir = 'C:\Program Files\Ahwoo\Games\Kitten Space Agency',
    [string[]]$Assemblies = @('KSA', 'Brutal.Core', 'Brutal.Numerics', 'Brutal.ImGui', 'Brutal.ImGui.Abstractions', 'Brutal.ImGui.Extensions', 'Planet.Core', 'Planet.Render.Core')
)

$ErrorActionPreference = 'Stop'
$outRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'decompiled'

foreach ($name in $Assemblies) {
    if ($name -notmatch '^[A-Za-z0-9_.-]+$' -or $name -in @('.', '..')) {
        throw "Invalid assembly name: $name"
    }
    $dll = Join-Path $GameDir "$name.dll"
    if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) {
        throw "Assembly not found: $dll"
    }
}
if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
    throw 'Install the decompiler first: dotnet tool install --global ilspycmd'
}

$versionFile = Join-Path $outRoot 'VERSION.txt'
if (Test-Path -LiteralPath $versionFile) {
    Remove-Item -LiteralPath $versionFile
}

foreach ($name in $Assemblies) {
    $dll = Join-Path $GameDir "$name.dll"
    $outDir = Join-Path $outRoot $name
    if (Test-Path -LiteralPath $outDir) {
        Remove-Item -LiteralPath $outDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $outDir | Out-Null
    Write-Host "Decompiling $dll -> $outDir"
    ilspycmd --disable-updatecheck -p -o $outDir -r $GameDir $dll
    if ($LASTEXITCODE -ne 0) {
        throw "ilspycmd failed for $name"
    }
}

$version = (Get-Item -LiteralPath (Join-Path $GameDir 'KSA.dll')).VersionInfo.ProductVersion
Set-Content -LiteralPath $versionFile -Value "KSA $version from $GameDir decompiled $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
Write-Host "Done. Game version: $version"
