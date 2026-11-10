# NeuroCompanion — Unity MCP Setup Script for Antigravity
# Run this ONCE after cloning the repo and installing Unity CLI.
# It configures the Unity MCP connection for Antigravity on your machine.
#
# Requirements before running:
#   1. Unity Hub installed
#   2. Unity 6000.3.28f1 (Unity 6.3 LTS) installed via Unity Hub
#   3. Unity CLI installed: irm https://unity.com/install.ps1 | iex
#   4. This repo cloned somewhere on your machine
#   5. The NeuroCompanion Unity project OPENED in Unity Editor
#
# Usage:
#   Open PowerShell, cd to the repo root, then run:
#   .\setup\setup_unity_mcp.ps1
#
# Optional: Pass your Unity project path if it's not in the default location
#   .\setup\setup_unity_mcp.ps1 -ProjectPath "D:\NeuroCompanion\Unity\NeuroCompanion"

param(
    [string]$ProjectPath = ""
)

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  NeuroCompanion — Unity MCP Setup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# ── Step 1: Find Unity CLI ────────────────────────────────────────────────────
Write-Host "[1/5] Checking Unity CLI..." -ForegroundColor Yellow
$unityCLI = Get-Command unity -ErrorAction SilentlyContinue
if (-not $unityCLI) {
    Write-Host "  Unity CLI not found. Installing now..." -ForegroundColor Red
    irm https://unity.com/install.ps1 | iex
    # Reload PATH
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH","User")
    $unityCLI = Get-Command unity -ErrorAction SilentlyContinue
    if (-not $unityCLI) {
        Write-Host "  ERROR: Unity CLI install failed. Please install manually:" -ForegroundColor Red
        Write-Host "    irm https://unity.com/install.ps1 | iex" -ForegroundColor White
        Write-Host "  Then open a new PowerShell window and re-run this script." -ForegroundColor White
        exit 1
    }
}
$unityVersion = unity --version 2>&1
Write-Host "  Unity CLI found: $unityVersion" -ForegroundColor Green

# ── Step 2: Determine Unity project path ─────────────────────────────────────
Write-Host "[2/5] Locating Unity project..." -ForegroundColor Yellow

if ($ProjectPath -eq "") {
    # Try to find the project relative to this script's repo root
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $repoRoot   = Split-Path -Parent $scriptDir
    $defaultPath = Join-Path $repoRoot "Unity\NeuroCompanion"

    if (Test-Path $defaultPath) {
        $ProjectPath = (Resolve-Path $defaultPath).Path
    } else {
        Write-Host ""
        Write-Host "  Could not auto-detect the Unity project path." -ForegroundColor Red
        Write-Host "  Please enter the full path to the Unity project folder." -ForegroundColor White
        Write-Host "  (The folder that contains Assets/, Packages/, ProjectSettings/)" -ForegroundColor Gray
        $ProjectPath = Read-Host "  Project path"
    }
}

if (-not (Test-Path $ProjectPath)) {
    Write-Host "  ERROR: Path not found: $ProjectPath" -ForegroundColor Red
    Write-Host "  Make sure you have opened the project in Unity Editor at least once." -ForegroundColor White
    exit 1
}
Write-Host "  Project path: $ProjectPath" -ForegroundColor Green

# ── Step 3: Find Unity Hub / Unity runtime ────────────────────────────────────
Write-Host "[3/5] Locating Unity CLI executable..." -ForegroundColor Yellow

# unity.exe is the CLI binary (not the Unity Editor). It lives next to Unity Hub.
$possibleCLIPaths = @(
    "$env:LOCALAPPDATA\Programs\Unity\Hub\resources\unity.exe",
    "C:\Program Files\Unity\Hub\resources\unity.exe",
    "D:\UnityHub\resources\unity.exe",
    (Get-Command unity -ErrorAction SilentlyContinue)?.Source
) | Where-Object { $_ -and (Test-Path $_) }

$cliExePath = $possibleCLIPaths | Select-Object -First 1

if (-not $cliExePath) {
    # Fall back: resolve from PATH
    $cliExePath = (Get-Command unity).Source
}

Write-Host "  CLI executable: $cliExePath" -ForegroundColor Green

# ── Step 4: Write the Antigravity MCP config ─────────────────────────────────
Write-Host "[4/5] Writing Antigravity MCP config..." -ForegroundColor Yellow

$configDir  = "$env:USERPROFILE\.gemini\config"
$configFile = "$configDir\mcp_config.json"

# Create directory if it doesn't exist
if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
}

# Build config object
$config = @{
    mcpServers = @{
        unity = @{
            command = $cliExePath
            args    = @("mcp", "--project-path", $ProjectPath)
        }
    }
}

# If the file already exists, merge unity entry instead of overwriting everything
if (Test-Path $configFile) {
    try {
        $existing = Get-Content $configFile -Raw | ConvertFrom-Json
        if (-not $existing.mcpServers) {
            $existing | Add-Member -MemberType NoteProperty -Name "mcpServers" -Value @{}
        }
        $existing.mcpServers | Add-Member -MemberType NoteProperty -Name "unity" -Value $config.mcpServers.unity -Force
        $config = $existing
        Write-Host "  Merged into existing config (other MCP servers preserved)." -ForegroundColor Cyan
    } catch {
        Write-Host "  Existing config unreadable — will overwrite." -ForegroundColor Yellow
    }
}

$config | ConvertTo-Json -Depth 10 | Set-Content $configFile -Encoding UTF8
Write-Host "  Config written to: $configFile" -ForegroundColor Green

# ── Step 5: Verify ───────────────────────────────────────────────────────────
Write-Host "[5/5] Verifying configuration..." -ForegroundColor Yellow
$written = Get-Content $configFile -Raw | ConvertFrom-Json
$unityEntry = $written.mcpServers.unity
if ($unityEntry) {
    Write-Host "  unity MCP entry confirmed in config." -ForegroundColor Green
    Write-Host "  Command : $($unityEntry.command)" -ForegroundColor Gray
    Write-Host "  Args    : $($unityEntry.args -join ' ')" -ForegroundColor Gray
} else {
    Write-Host "  ERROR: Config write failed. Please check manually: $configFile" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  SETUP COMPLETE!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Make sure the NeuroCompanion Unity project is OPEN in Unity Editor"
Write-Host "  2. RESTART Antigravity completely (close and reopen)"
Write-Host "  3. In Antigravity, the 'unity' MCP tools will now be available"
Write-Host ""
Write-Host "To verify the connection after restart, ask Antigravity:" -ForegroundColor Cyan
Write-Host "  'List the available Unity MCP tools'" -ForegroundColor White
Write-Host ""
