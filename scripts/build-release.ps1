param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "ArbuzTweaker\ArbuzTweaker.csproj"
$installerProjectPath = Join-Path $repoRoot "Installer\ArbuzTweaker.Installer.wixproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsRoot "publish"
$installerBuildDir = Join-Path $artifactsRoot "installer-build"
$portableZipPath = Join-Path $artifactsRoot "ArbuzTweaker-Portable.zip"
$installerPath = Join-Path $artifactsRoot "ArbuzTweaker-Setup.msi"

if (Test-Path $artifactsRoot) {
    Remove-Item $artifactsRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $artifactsRoot | Out-Null

$projectXml = [xml](Get-Content -LiteralPath $projectPath -Raw)
$version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Project version is missing in $projectPath"
}

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $publishDir

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZipPath -Force

dotnet build $installerProjectPath `
    -c $Configuration `
    -p:ProductVersion=$version `
    -p:PublishDir=$publishDir `
    -p:RepoRoot=$repoRoot `
    -o $installerBuildDir

$builtInstaller = Get-ChildItem -Path $installerBuildDir -Filter "ArbuzTweaker-Setup.msi" -Recurse | Select-Object -First 1
if ($builtInstaller -eq $null) {
    throw "MSI installer was not produced in $installerBuildDir"
}

Copy-Item -LiteralPath $builtInstaller.FullName -Destination $installerPath -Force

Write-Host "Portable zip: $portableZipPath"
Write-Host "MSI installer: $installerPath"
