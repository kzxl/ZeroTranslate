$ErrorActionPreference = "Stop"

$ProjectDir = "ZeroTranslate"
$ProjectFile = "$ProjectDir\ZeroTranslate.csproj"
$OutputBase = "PublishOutput"

Write-Host "ZeroTranslate Build Optimizations Script" -ForegroundColor Cyan
Write-Host "====================================="

# 1. Clean previous build
Write-Host "Cleaning old builds..." -ForegroundColor Yellow
if (Test-Path $OutputBase) {
    Remove-Item -Recurse -Force $OutputBase
}
New-Item -ItemType Directory -Force -Path "$OutputBase\Lightweight" | Out-Null
New-Item -ItemType Directory -Force -Path "$OutputBase\Standalone" | Out-Null

# 2. Build Lightweight version (Not self-contained, requires .NET 8 on target machine)
Write-Host "`nBuilding [Lightweight] Mode (Fast to download, requires .NET 8 runtime)..." -ForegroundColor Green
dotnet publish $ProjectFile -c Release -r win-x64 -p:SelfContained=false -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "$OutputBase\Lightweight"

# 3. Build Standalone version (Self-contained, huge but portable -> Now compressed!)
Write-Host "`nBuilding [Standalone] Mode (Includes .NET 8, compressed to save space)..." -ForegroundColor Green
dotnet publish $ProjectFile -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=true -o "$OutputBase\Standalone"

Write-Host "`nBuild Complete!" -ForegroundColor Cyan
Write-Host "Check the $OutputBase directory for the generated files."
