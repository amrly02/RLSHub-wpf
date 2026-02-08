# Upload published RLSHub.Wpf.exe to GitHub Releases (asks first).
# Requires: GitHub CLI (gh) installed and logged in.
# Runs automatically after dotnet publish; or run .\upload-release.ps1 manually.

$ErrorActionPreference = "Stop"
# Skip prompt when run non-interactively (e.g. CI)
if (-not [Environment]::UserInteractive) { exit 0 }
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $scriptDir "bin\Release\net8.0-windows\win-x64\publish"
$exePath = Join-Path $publishDir "RLSHub.Wpf.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "Publish output not found: $exePath" -ForegroundColor Red
    Write-Host "Run publish first: dotnet publish -c Release -p:PublishProfile=win-x64-singlefile -p:Platform=x64"
    exit 1
}

$sizeMb = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
Write-Host "Found: RLSHub.Wpf.exe ($sizeMb MB)" -ForegroundColor Green
Write-Host ""
$answer = Read-Host "Upload this build to GitHub Releases? (y/n)"
if ($answer -notmatch '^[yY]') {
    Write-Host "Skipped."
    exit 0
}

# Check gh
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "GitHub CLI (gh) is not installed. Install: winget install GitHub.cli" -ForegroundColor Red
    exit 1
}

$tag = Read-Host "Release tag (e.g. v1.0.0)"
if ([string]::IsNullOrWhiteSpace($tag)) {
    Write-Host "Tag is required."
    exit 1
}

$notes = Read-Host "Release notes (optional; press Enter to skip)"
$title = "RLSHub WPF $tag"

$createArgs = @(
    "release", "create", $tag,
    "--title", $title,
    $exePath
)
if (-not [string]::IsNullOrWhiteSpace($notes)) {
    $createArgs += "--notes"
    $createArgs += $notes
}

Write-Host ""
Write-Host "Creating release $tag and uploading RLSHub.Wpf.exe..." -ForegroundColor Cyan
& gh @createArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Upload failed (e.g. tag already exists or not authenticated)." -ForegroundColor Red
    exit 1
}
Write-Host "Done. Release: https://github.com/$(gh repo view --json nameWithOwner -q .nameWithOwner)/releases/tag/$tag" -ForegroundColor Green
