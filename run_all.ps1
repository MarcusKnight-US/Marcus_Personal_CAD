# run_all.ps1 - Batch convert all example DWG files to PNG using DwgToPngConverter on Windows

$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) {
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}
$DwgExamplesDir = Join-Path $ScriptDir "dwg_examples"
$OutputDir = Join-Path $ScriptDir "dwg_output"
$ProjectDir = Join-Path $ScriptDir "DwgToPngConverter"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    Write-Host "Created output directory: $OutputDir"
}

$DwgFiles = Get-ChildItem -Path $DwgExamplesDir -Filter *.dwg
Write-Host "Found $($DwgFiles.Count) DWG files to convert."

$SuccessCount = 0
$FailCount = 0

foreach ($File in $DwgFiles) {
    $Filename = $File.Name
    $PngName = [System.IO.Path]::ChangeExtension($Filename, "png")
    $OutPath = Join-Path $OutputDir $PngName
    
    Write-Host ""
    Write-Host "--------------------------------------------------"
    Write-Host "Converting: $Filename"
    Write-Host "To: $OutPath"

    # Forward any arguments passed to run_all.ps1 (e.g. --debug)
    $DotnetArgs = @("run", "--project", $ProjectDir, "--", $File.FullName, $OutPath)
    if ($args) {
        $DotnetArgs += $args
    }

    & dotnet $DotnetArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "SUCCESS: $Filename converted."
        $SuccessCount++
    } else {
        Write-Host "FAILED: $Filename failed to convert."
        $FailCount++
    }
}

Write-Host ""
Write-Host "=================================================="
Write-Host "Batch conversion completed!"
Write-Host "Success: $SuccessCount"
Write-Host "Failed: $FailCount"
Write-Host "Output folder: $OutputDir"
Write-Host "=================================================="
