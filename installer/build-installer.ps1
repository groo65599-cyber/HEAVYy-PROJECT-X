# ============================================================================
#  HPX PIRATE - Windows build helper
#  Publishes the app and compiles the Inno Setup installer.
#
#  Usage (from the repository root, in an elevated PowerShell):
#      powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
#
#  Optional:
#      -Configuration Release
#      -Runtime win-x64
#      -SkipPublish        (reuse an existing .\publish folder)
#      -SkipInstaller      (only publish, do not build the setup)
# ============================================================================

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64",
    [switch]$SkipPublish,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot   # repo root (parent of \installer)

Write-Host "== HPX PIRATE build ==" -ForegroundColor Magenta
Write-Host "Root         : $root"
Write-Host "Configuration: $Configuration"
Write-Host "Runtime      : $Runtime"

# --- 1. Publish the WPF application -----------------------------------------
if (-not $SkipPublish) {
    Write-Host "`n[1/3] Publishing application..." -ForegroundColor Cyan
    $proj = Join-Path $root "src\HPXPirate\HPXPirate.csproj"
    $out  = Join-Path $root "publish"
    & dotnet publish $proj -c $Configuration -r $Runtime --self-contained false -o $out
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }
} else {
    Write-Host "`n[1/3] Publish skipped." -ForegroundColor Yellow
}

# --- 2. Locate the Inno Setup compiler --------------------------------------
if (-not $SkipInstaller) {
    Write-Host "`n[2/3] Locating Inno Setup compiler (iscc.exe)..." -ForegroundColor Cyan
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        $iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
    }
    if (-not $iscc) {
        throw "Inno Setup compiler (iscc.exe) not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php"
    }
    Write-Host "Using: $iscc"

    # --- 3. Compile the installer -------------------------------------------
    Write-Host "`n[3/3] Compiling installer..." -ForegroundColor Cyan
    $iss = Join-Path $PSScriptRoot "HPX-PIRATE.iss"
    & $iscc $iss
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }

    $setup = Join-Path $root "dist\HPX-PIRATE-Setup.exe"
    if (Test-Path $setup) {
        Write-Host "`nSUCCESS -> $setup" -ForegroundColor Green
    } else {
        Write-Host "`nInstaller compiled (check .\dist)." -ForegroundColor Green
    }
} else {
    Write-Host "`n[2/3] Installer build skipped." -ForegroundColor Yellow
}
