#!/usr/bin/env bash

# Bash script to batch convert all example DWG files to PNG using DwgToPngConverter

# Get directory of this script
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DWG_EXAMPLES_DIR="$SCRIPT_DIR/dwg_examples"
OUTPUT_DIR="$SCRIPT_DIR/dwg_output"
PROJECT_DIR="$SCRIPT_DIR/DwgToPngConverter"

if [ ! -d "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR"
    echo "Created output directory: $OUTPUT_DIR"
fi

# Detect dotnet command (use dotnet.exe under WSL if Linux dotnet isn't installed)
DOTNET_CMD="dotnet"
IS_WINDOWS_DOTNET=false

if ! command -v "$DOTNET_CMD" &> /dev/null; then
    if command -v "dotnet.exe" &> /dev/null; then
        DOTNET_CMD="dotnet.exe"
        IS_WINDOWS_DOTNET=true
        echo "Using Windows host dotnet executable: dotnet.exe"
    else
        echo "ERROR: dotnet SDK command not found. Please install the .NET SDK." >&2
        exit 1
    fi
fi

# Enable case-insensitive globbing and nullglob
shopt -s nullglob
shopt -s nocaseglob
dwg_files=("$DWG_EXAMPLES_DIR"/*.dwg)

echo "Found ${#dwg_files[@]} DWG files to convert."

success_count=0
fail_count=0

for file in "${dwg_files[@]}"; do
    filename=$(basename "$file")
    png_name="${filename%.*}.png"
    out_path="$OUTPUT_DIR/$png_name"
    
    echo ""
    echo "--------------------------------------------------"
    echo "Converting: $filename"
    echo "To: $out_path"
    
    # Run the .NET application with forwarded arguments
    if [ "$IS_WINDOWS_DOTNET" = true ]; then
        # Convert WSL paths to Windows paths for dotnet.exe
        win_project_dir=$(wslpath -w "$PROJECT_DIR" 2>/dev/null || echo "$PROJECT_DIR")
        win_file=$(wslpath -w "$file" 2>/dev/null || echo "$file")
        win_out_path=$(wslpath -w "$out_path" 2>/dev/null || echo "$out_path")
        
        "$DOTNET_CMD" run --project "$win_project_dir" -- "$win_file" "$win_out_path" "$@"
    else
        # Native Linux/macOS execution
        "$DOTNET_CMD" run --project "$PROJECT_DIR" -- "$file" "$out_path" "$@"
    fi
    
    if [ $? -eq 0 ]; then
        echo "SUCCESS: $filename converted."
        success_count=$((success_count + 1))
    else
        echo "FAILED: $filename failed to convert."
        fail_count=$((fail_count + 1))
    fi
done

echo ""
echo "=================================================="
echo "Batch conversion completed!"
echo "Success: $success_count"
echo "Failed: $fail_count"
echo "Output folder: $OUTPUT_DIR"
echo "=================================================="
