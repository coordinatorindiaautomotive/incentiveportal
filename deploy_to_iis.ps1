$ErrorActionPreference = "Stop"

$ProjectName = "IncentivePortal"
$ProjectPath = ".\IncentivePortal.csproj"
$OutputDirectory = ".\bin\Release\net10.0\win-x64\publish"
$ZipFileName = "IncentivePortal_Release.zip"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Incentive Portal IIS Deployment      " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`n[1/4] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $OutputDirectory) {
    Remove-Item -Path $OutputDirectory -Recurse -Force
}
if (Test-Path $ZipFileName) {
    Remove-Item -Path $ZipFileName -Force
}

Write-Host "`n[2/4] Restoring NuGet Packages..." -ForegroundColor Yellow
dotnet restore $ProjectPath

Write-Host "`n[3/4] Publishing Self-Contained Release for IIS (win-x64)..." -ForegroundColor Yellow
# We use self-contained=true so the IIS server doesn't need .NET 10 installed
dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true -o $OutputDirectory

Write-Host "`n[4/4] Zipping artifacts for deployment..." -ForegroundColor Yellow
Compress-Archive -Path "$OutputDirectory\*" -DestinationPath $ZipFileName -Force

Write-Host "`n========================================" -ForegroundColor Green
Write-Host " BUILD SUCCESSFUL!                      " -ForegroundColor Green
Write-Host " Deployment Package: $ZipFileName       " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
