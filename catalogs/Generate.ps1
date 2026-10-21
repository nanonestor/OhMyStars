param(
    [string]$SourceRoot = 'D:\ksa\stars\catalogs'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\tools\CatalogBuilder\CatalogBuilder.csproj'

& dotnet run --project $project -- --self-test
if ($LASTEXITCODE -ne 0) { throw 'CatalogBuilder self-tests failed.' }
& dotnet run --no-build --project $project -- $SourceRoot $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'Catalog generation failed.' }
& dotnet run --no-build --project $project -- --validate $SourceRoot $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'Catalog validation failed.' }
