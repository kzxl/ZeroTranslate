<#
    publish.ps1 — Publish script for ZeroTranslate (Dual Mode: Full & Lite)
    Adheres to AgentOption .NET Publish Release standard & ZeroUniverse rules.
#>
[CmdletBinding()]
param(
    [ValidateSet('Full', 'Lite', 'All')]
    [string]$Mode = 'All',
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Proj = Join-Path $Root "ZeroTranslate\ZeroTranslate.csproj"
$Dist = Join-Path $Root "publish"

if (Test-Path $Dist) {
    Remove-Item $Dist -Recurse -Force -ErrorAction SilentlyContinue
}

if ($Mode -eq 'Full' -or $Mode -eq 'All') {
    Write-Host ">>> Publishing ZeroTranslate FULL (Self-Contained Single File)..." -ForegroundColor Cyan
    $outFull = Join-Path $Dist "full"
    dotnet publish $Proj -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishReadyToRun=true `
        -o $outFull
    Write-Host "  ✔ Full build generated at: $outFull\ZeroTranslate.exe" -ForegroundColor Green
}

if ($Mode -eq 'Lite' -or $Mode -eq 'All') {
    Write-Host ">>> Publishing ZeroTranslate LITE (Framework-Dependent Single File)..." -ForegroundColor Cyan
    $outLite = Join-Path $Dist "lite"
    dotnet publish $Proj -c $Configuration -r $Runtime --self-contained false `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=true `
        -o $outLite
    Write-Host "  ✔ Lite build generated at: $outLite\ZeroTranslate.exe" -ForegroundColor Green
}

Write-Host ">>> ZeroTranslate publish completed successfully!" -ForegroundColor Green
