# generate_line_by_line_docs_fixed.ps1

# PowerShell script to create line‑by‑line Markdown documentation for every .cs file in the DWGtoPNG project.
# Output format:
#   Line #: <code>
#   // Explanation for line <n>
# Files are written to the sibling 'docs' folder with the same base name as the source file.

param(
    [string]$SourceRoot = "$(Split-Path -Parent $MyInvocation.MyCommand.Path)\..",
    [string]$DocsRoot   = "$(Split-Path -Parent $MyInvocation.MyCommand.Path)"
)

function Write-Doc {
    param([string]$sourcePath)
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($sourcePath)
    $mdPath   = Join-Path $DocsRoot "${fileName}.md"
    $lines    = Get-Content -Path $sourcePath -Raw -Encoding utf8 -ErrorAction Stop -Split "`n"
    $md = @()
    $md += "# $fileName.cs – Line‑by‑Line Explanation"
    $md += ""
    $md += "```csharp"
    $lineNumber = 1
    foreach ($ln in $lines) {
        $escaped = $ln -replace "`r", ""   # strip CR
        $md += "$lineNumber: $escaped"
        $md += "// Explanation for line $lineNumber"
        $lineNumber++
    }
    $md += "```"
    Set-Content -Path $mdPath -Value $md -Encoding utf8
    Write-Host "Generated $mdPath"
}

# Ensure docs folder exists
if (-not (Test-Path $DocsRoot)) { New-Item -ItemType Directory -Path $DocsRoot | Out-Null }

# Find all .cs files recursively and generate docs
Get-ChildItem -Path $SourceRoot -Recurse -Filter "*.cs" -File | ForEach-Object { Write-Doc $_.FullName }

# Create index.md linking to all generated docs
$indexPath = Join-Path $DocsRoot "index.md"
$index = @()
$index += "# Documentation Index"
$index += ""
$index += "Line‑by‑line Markdown files for every C# source file in the project."
$index += ""
$index += "## Files"
$index += ""
Get-ChildItem -Path $DocsRoot -Filter "*.md" -File | Where-Object { $_.Name -ne "index.md" } | Sort-Object Name | ForEach-Object {
    $relative = $_.Name
    $index += "- [$relative]($relative)"
}
Set-Content -Path $indexPath -Value $index -Encoding utf8
Write-Host "Generated index at $indexPath"

# End of script
