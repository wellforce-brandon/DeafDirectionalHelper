# Post-Edit Hook for .NET Projects
# Runs after Claude edits files to verify build status

param(
    [string]$ProjectPath = "."
)

$ErrorActionPreference = "Continue"

Write-Host "`n=== Post-Edit Check ===" -ForegroundColor Cyan

# Find .csproj file
$csproj = Get-ChildItem -Path $ProjectPath -Filter "*.csproj" -Recurse | Select-Object -First 1

if (-not $csproj) {
    Write-Host "No .csproj found, skipping build check" -ForegroundColor Yellow
    exit 0
}

Write-Host "Building $($csproj.Name)..." -ForegroundColor Gray

# Run build
$buildResult = & dotnet build $csproj.FullName --nologo -v q 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build: SUCCESS" -ForegroundColor Green
} else {
    Write-Host "Build: FAILED" -ForegroundColor Red
    Write-Host "Errors:" -ForegroundColor Yellow
    $buildResult | Where-Object { $_ -match "error" } | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Red
    }
}

Write-Host "========================`n" -ForegroundColor Cyan
