# generate_line_by_line_docs.ps1

# This PowerShell script scans the DWGtoPNG source tree and creates, for every *.cs file, a Markdown document
# that lists each line of source code together with a placeholder for a line‑by‑line explanation.
# The script follows the user‑requested format:
#   Line #: <code>
#   // explanation (on the next line)
# The generated files are placed under the `docs/` folder next to this script.

param(
    [string]$SourceRoot = "$(Split-Path -Parent $MyInvocation.MyCommand.Path)\..",
    [string]$DocsRoot   = "$(Split-Path -Parent $MyInvocation.MyCommand.Path)"
)

function Write‑Doc ([string]$sourcePath) {
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($sourcePath)
    $mdPath   = Join-Path $DocsRoot "${fileName}.md"
    $lines    = Get-Content -Path $sourcePath -Raw -Encoding utf8 -ErrorAction Stop -Split "`n"

    $md = @()
    $md += "# $fileName.cs – Line‑by‑Line Explanation"
    $md += ""
    $md += "```csharp"
    $lineNumber = 1
    foreach ($ln in $lines) {
        $escaped = $ln -replace "`r", ""   # remove carriage returns
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

# Recursively find all .cs files in the source root
Get-ChildItem -Path $SourceRoot -Recurse -Filter "*.cs" -File | ForEach-Object { Write‑Doc $_.FullName }

# Create an index file linking to all generated docs
$indexPath = Join-Path $DocsRoot "index.md"
$index = @()
$index += "# Documentation Index"
$index += "" 
$index += "This index lists a line‑by‑line Markdown document for every C# source file in the project."
$index += ""
$index += "## Files"
$index += ""
Get-ChildItem -Path $DocsRoot -Filter "*.md" -File | Where-Object { $_.Name -ne "index.md" } | Sort-Object Name | ForEach-Object {
    $link = "[${_.BaseName}](${_.FullName})"
    $index += "- $link"
}
Set-Content -Path $indexPath -Value $index -Encoding utf8
Write-Host "Generated index at $indexPath"

# End of script
