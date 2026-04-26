param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[FROST] $Message" -ForegroundColor Cyan
}

function Get-IsccPath {
    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $commonPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )

    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}

function Install-InnoSetup {
    Write-Step "Installing Inno Setup via winget..."
    winget install --exact --id JRSoftware.InnoSetup --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        throw "winget failed to install Inno Setup."
    }
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

$dotnetCliHome = Join-Path $repoRoot ".dotnetcli"
New-Item -ItemType Directory -Force -Path $dotnetCliHome | Out-Null
$env:DOTNET_CLI_HOME = $dotnetCliHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"

$publishDir = Join-Path $repoRoot "release\publish\$Runtime"
$outputDir = Join-Path $repoRoot "release\installer"
$issPath = Join-Path $repoRoot "installer\FROST.iss"

Write-Step "Publishing FROST ($Configuration / $Runtime)..."
dotnet publish "FROST.csproj" -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$iscc = Get-IsccPath
if (-not $iscc) {
    Install-InnoSetup
    $iscc = Get-IsccPath
}

if (-not $iscc) {
    throw "ISCC.exe was not found after installing Inno Setup."
}

Write-Step "Building installer..."
& $iscc "/DPublishDir=$publishDir" "/DOutputDir=$outputDir" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed."
}

if (Test-Path $outputDir) {
    $installer = Get-ChildItem -Path $outputDir -Filter "*_setup.exe" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

Write-Step "Installer ready:"
if ($installer) {
    Write-Host $installer.FullName -ForegroundColor Green
}
else {
    Write-Host $outputDir -ForegroundColor Green
}
