param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Run
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[FROST] $Message" -ForegroundColor Cyan
}

function Test-WindowsHost {
    return [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
}

function Get-DotNetCommand {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $fallback = Join-Path ${env:ProgramFiles} "dotnet\dotnet.exe"
    if (Test-Path $fallback) {
        return $fallback
    }

    return $null
}

function Test-DotNetSdk8Installed {
    $dotnet = Get-DotNetCommand
    if (-not $dotnet) {
        return $false
    }

    $sdks = & $dotnet --list-sdks 2>$null
    return $sdks -match '^\s*8\.'
}

function Get-SetupRequirements {
    param([string]$RequirementsPath)

    $entries = Get-Content $RequirementsPath |
        Where-Object { $_.Trim() -and -not $_.Trim().StartsWith("#") }

    if (-not $entries) {
        throw "requirements.txt is empty."
    }

    return @($entries | ForEach-Object { $_.Trim() })
}

if (-not (Test-WindowsHost)) {
    throw "FROST requires Windows."
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

$dotnetCliHome = Join-Path $repoRoot ".dotnetcli"
New-Item -ItemType Directory -Force -Path $dotnetCliHome | Out-Null
$env:DOTNET_CLI_HOME = $dotnetCliHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"

$requirementsPath = Join-Path $repoRoot "requirements.txt"
if (-not (Test-Path $requirementsPath)) {
    throw "Missing requirements.txt next to setup.ps1."
}

$requirements = Get-SetupRequirements -RequirementsPath $requirementsPath
if ($requirements.Count -lt 1) {
    throw "requirements.txt must contain the .NET SDK package ID."
}

$dotnetWingetId = $requirements[0]

if (-not (Test-DotNetSdk8Installed)) {
    Write-Step ".NET 8 SDK not found. Installing $dotnetWingetId via winget..."

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw "winget is not available. Install .NET 8 SDK manually, then rerun setup.ps1."
    }

    & $winget.Source install --exact --id $dotnetWingetId --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "winget failed to install $dotnetWingetId."
    }
}
else {
    Write-Step ".NET 8 SDK already installed."
}

$dotnet = Get-DotNetCommand
if (-not $dotnet) {
    throw "dotnet was not found after setup."
}

Write-Step "Restoring FROST..."
& $dotnet restore "FROST.sln"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed."
}

Write-Step "Building FROST ($Configuration)..."
& $dotnet build "FROST.sln" -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed."
}

if ($Run) {
    Write-Step "Launching FROST..."
    & $dotnet run --project "FROST.csproj" -c $Configuration
    exit $LASTEXITCODE
}

Write-Step "Setup complete."
Write-Host "Run .\setup.bat to build and launch FROST in one click." -ForegroundColor Green
