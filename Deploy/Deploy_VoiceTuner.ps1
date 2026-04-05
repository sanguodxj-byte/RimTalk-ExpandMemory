# SCRIPT TO DEPLOY THE 'VoiceTuner' MOD
# 1. Manually compile your mod in Visual Studio first.
# 2. EDIT the $sourceDir variable below to point to your VoiceTuner project's root directory.
# 3. RUN this script from PowerShell to deploy the mod.

# --- CONFIGURATION ---
# !!! IMPORTANT !!!
# Set this variable to the root directory of your VoiceTuner mod project.
# This is the folder that contains the 'About', '1.5', 'Source', etc. folders.
$sourceDir = "D:\Path\To\Your\VoiceTuner\Project"

# This is the target directory where the mod will be installed.
$modName = "VoiceTuner"
$rimworldModsDir = "D:\steam\steamapps\common\RimWorld\Mods"
$targetDir = Join-Path $rimworldModsDir $modName

# --- DEPLOYMENT LOGIC ---
Write-Host "Starting deployment of '$modName'..." -ForegroundColor Green

if (($sourceDir -eq "D:\Path\To\Your\VoiceTuner\Project") -or -not (Test-Path $sourceDir)) {
    Write-Error "SOURCE DIRECTORY NOT SET OR NOT FOUND: '$sourceDir'. Please edit this script and set the correct project path."
    exit 1
}

Write-Host "Source: $sourceDir"
Write-Host "Target: $targetDir"

# Ensure a clean deployment by removing the old mod directory first.
Write-Host "Cleaning target directory..."
if (Test-Path $targetDir) {
    Remove-Item -Recurse -Force $targetDir
}
New-Item -ItemType Directory -Path $targetDir | Out-Null

Write-Host "Copying mod files..."
# Robocopy is used for its speed and ability to exclude development-related files.
# This will copy everything and then exclude files/directories not needed in the final mod.
# You can adjust the /XD (Exclude Directory) and /XF (Exclude File) parameters as needed.
robocopy $sourceDir $targetDir /E `
    /XD ".git" ".vs" "obj" "bin" "Source" ".github" `
    /XF "*.csproj" "*.sln" "*.user" "*.ps1" "README.md" `
    /NFL /NJS /NJH /NDL # Suppress verbose output

if ($LASTEXITCODE -lt 4) {
    Write-Host "Deployment of '$modName' complete!" -ForegroundColor Green
} else {
    Write-Error "Robocopy encountered an error (Code: $LASTEXITCODE). Please check the output."
}