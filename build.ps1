<#
    Builds and versions both deployable artifacts on every run: the Android APK and the server
    Docker image (+ the docker-compose.yaml that ties it together with Radicale). No change
    detection by design - both artifacts are always rebuilt, even if only one side changed.

    version.json uses plain SemVer; the patch number is incremented by 0.0.1 on every run.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [string] $Description,
        [Parameter(Mandatory)] [scriptblock] $Command
    )

    Write-Host ">>> $Description" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

docker info | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Docker does not appear to be running - it's required to build the server container image."
}

# --- 1. Version bump: pure SemVer, patch +1 every run ---
$versionFile = Join-Path $repoRoot "version.json"
if (-not (Test-Path $versionFile)) {
    '{ "major": 0, "minor": 1, "patch": 0 }' | Set-Content $versionFile -Encoding utf8
}
$version = Get-Content $versionFile -Raw | ConvertFrom-Json
$version.patch = [int]$version.patch + 1
($version | ConvertTo-Json) | Set-Content $versionFile -Encoding utf8

$semVer = "$($version.major).$($version.minor).$($version.patch)"
$androidVersionCode = [int]$version.major * 10000 + [int]$version.minor * 100 + [int]$version.patch

Write-Host "=== FamilyCloud build $semVer ===" -ForegroundColor Green

$artifactsDir = Join-Path $repoRoot "artifacts"
New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

# --- 2. Android APK - always built ---
$appOutDir = Join-Path $artifactsDir "app"
Invoke-Checked "Building Android APK ($semVer)" {
    dotnet publish (Join-Path $repoRoot "src/FamilyCloud.App/FamilyCloud.App.csproj") `
        -f net10.0-android -c Release `
        -p:ApplicationDisplayVersion=$semVer `
        -p:ApplicationVersion=$androidVersionCode `
        -o $appOutDir
}

$signedApk = Get-ChildItem $appOutDir -Filter "*-Signed.apk" | Select-Object -First 1
if (-not $signedApk) {
    throw "Expected a signed APK under $appOutDir but found none."
}
$finalApkPath = Join-Path $appOutDir "FamilyCloud.App-$semVer.apk"
Copy-Item $signedApk.FullName $finalApkPath -Force

# --- 3. Server Docker image - always built ---
Invoke-Checked "Building server container image (familycloud-server:$semVer)" {
    dotnet publish (Join-Path $repoRoot "src/FamilyCloud.Server/FamilyCloud.Server.csproj") `
        -c Release --os linux --arch x64 -t:PublishContainer `
        -p:ContainerRepository=familycloud-server `
        -p:ContainerImageTag=$semVer
}
Invoke-Checked "Tagging familycloud-server:latest" { docker tag "familycloud-server:$semVer" "familycloud-server:latest" }

# --- 4. docker-compose.yaml - always regenerated, committed at repo root ---
$composeOutDir = Join-Path $artifactsDir "compose"
Remove-Item $composeOutDir -Recurse -Force -ErrorAction SilentlyContinue
Invoke-Checked "Generating docker-compose.yaml" {
    dotnet dnx aspire.cli@13.5.0 -- publish `
        --apphost (Join-Path $repoRoot "src/FamilyCloud.AppHost/FamilyCloud.AppHost.csproj") `
        -o $composeOutDir --non-interactive --nologo
}
Copy-Item (Join-Path $composeOutDir "docker-compose.yaml") (Join-Path $repoRoot "docker-compose.yaml") -Force

Write-Host ""
Write-Host "=== Build $semVer complete ===" -ForegroundColor Green
Write-Host "APK:          $finalApkPath"
Write-Host "Docker image: familycloud-server:$semVer (also tagged :latest)"
Write-Host "Compose file: $(Join-Path $repoRoot 'docker-compose.yaml')"
Write-Host ""
Write-Host "Before 'docker compose up' on a deploy target, create a .env next to docker-compose.yaml" -ForegroundColor Yellow
Write-Host "(see .env.example) with at least:" -ForegroundColor Yellow
Write-Host "  SERVER_IMAGE=familycloud-server:$semVer"
Write-Host "  SERVER_PORT=8080"
Write-Host "  JWT_SIGNING_KEY=<random 32+ character string>"
Write-Host "  SEED_ADMIN_USERNAME=<username>"
Write-Host "  SEED_ADMIN_PASSWORD=<password>"
