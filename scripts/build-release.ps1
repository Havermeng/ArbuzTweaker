param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "ArbuzTweaker\ArbuzTweaker.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsRoot "publish"
$portableZipPath = Join-Path $artifactsRoot "ArbuzTweaker-Portable.zip"
$installerPath = Join-Path $artifactsRoot "ArbuzTweaker-Setup.exe"
$installerInputDir = Join-Path $artifactsRoot "installer-input"
$sedPath = Join-Path $artifactsRoot "arbuztweaker-installer.sed"

if (Test-Path $artifactsRoot) {
    Remove-Item $artifactsRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $artifactsRoot | Out-Null

dotnet publish $projectPath -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=false -o $publishDir

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portableZipPath -Force

New-Item -ItemType Directory -Path $installerInputDir | Out-Null
Copy-Item $portableZipPath (Join-Path $installerInputDir "payload.zip")

$installScript = @'
$ErrorActionPreference = "Stop"

$targetDir = Join-Path $env:LOCALAPPDATA "Programs\ArbuzTweaker"
$payloadZip = Join-Path $PSScriptRoot "payload.zip"

if (Test-Path $targetDir) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
} else {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

Expand-Archive -Path $payloadZip -DestinationPath $targetDir -Force

$exePath = Join-Path $targetDir "ArbuzTweaker.exe"
$desktopPath = [Environment]::GetFolderPath("Desktop")
$programsPath = [Environment]::GetFolderPath("Programs")

$shortcutTargets = @(
    (Join-Path $desktopPath "ArbuzTweaker.lnk"),
    (Join-Path $programsPath "ArbuzTweaker.lnk")
)

$shell = New-Object -ComObject WScript.Shell
foreach ($shortcutPath in $shortcutTargets) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $targetDir
    $shortcut.IconLocation = "$exePath,0"
    $shortcut.Description = "ArbuzTweaker"
    $shortcut.Save()
}

Start-Process -FilePath $exePath
'@
Set-Content -Path (Join-Path $installerInputDir "install.ps1") -Value $installScript -Encoding UTF8

$sourceDir = $installerInputDir.Replace("\", "\\")
$targetName = $installerPath.Replace("\", "\\")

$sedContent = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=ArbuzTweaker installed.
TargetName=$targetName
FriendlyName=ArbuzTweaker Setup
AppLaunched=powershell.exe -NoProfile -ExecutionPolicy Bypass -File install.ps1
PostInstallCmd=<None>
AdminQuietInstCmd=powershell.exe -NoProfile -ExecutionPolicy Bypass -File install.ps1
UserQuietInstCmd=powershell.exe -NoProfile -ExecutionPolicy Bypass -File install.ps1
SourceFiles=SourceFiles
[Strings]
FILE0=payload.zip
FILE1=install.ps1
[SourceFiles]
SourceFiles0=$sourceDir\\
[SourceFiles0]
%FILE0%=
%FILE1%=
"@

Set-Content -Path $sedPath -Value $sedContent -Encoding ASCII
Start-Process -FilePath "iexpress.exe" -ArgumentList "/N", $sedPath -Wait -NoNewWindow

Write-Host "Portable zip: $portableZipPath"
Write-Host "Installer: $installerPath"
