# Build script for Newt Service MSI installer

Write-Host "Building Newt Service (single-file)..." -ForegroundColor Cyan

# Clean publish folder
Remove-Item -Recurse -Force publish -ErrorAction SilentlyContinue

# Publish as single-file self-contained executables
dotnet publish NewtService.Worker -c Release -o publish `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -r win-x64

dotnet publish NewtService.Tray -c Release -o publish `
    -p:PublishSingleFile=true `
    -p:SelfContained=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -r win-x64

# Remove unnecessary files
Remove-Item publish\*.pdb -ErrorAction SilentlyContinue
Remove-Item publish\*.json -ErrorAction SilentlyContinue

Write-Host "Creating MSI installer..." -ForegroundColor Cyan

# Build MSI with WiX
Push-Location installer
wix build Package.wxs -o ..\NewtServiceSetup.msi
Pop-Location

if (Test-Path "NewtServiceSetup.msi") {
    $msi = Get-Item "NewtServiceSetup.msi"
    Write-Host ""
    Write-Host "Success! Created: $($msi.Name) ($([math]::Round($msi.Length / 1MB, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "Failed to create MSI" -ForegroundColor Red
}
