param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[FROST Setup] $Message" -ForegroundColor Cyan
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

$dotnetCliHome = Join-Path $repoRoot ".dotnetcli"
New-Item -ItemType Directory -Force -Path $dotnetCliHome | Out-Null
$env:DOTNET_CLI_HOME = $dotnetCliHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"

$publishDir = Join-Path $repoRoot "release\publish\$Runtime"
$setupPublishDir = Join-Path $repoRoot "release\modern-setup"
$setupProject = Join-Path $repoRoot "installer\FROST.Setup\FROST.Setup.csproj"
$finalSetup = Join-Path $repoRoot "release\FROST_v1.0.6_setup.exe"

Write-Step "Publishing FROST payload ($Configuration / $Runtime)..."
dotnet publish "FROST.csproj" -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for FROST."
}

Write-Step "Building modern custom setup..."
dotnet publish $setupProject -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o $setupPublishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for FROST Setup."
}

$builtSetup = Join-Path $setupPublishDir "FROST_v1.0.6_setup.exe"
if (-not (Test-Path $builtSetup)) {
    throw "Built setup not found: $builtSetup"
}

Copy-Item -LiteralPath $builtSetup -Destination $finalSetup -Force

Write-Step "Modern setup ready:"
Write-Host $finalSetup -ForegroundColor Green
