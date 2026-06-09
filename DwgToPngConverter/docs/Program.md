# Program.cs

## Purpose
`Program.cs` contains the **entry point** of the application (`static void Main`). It parses command‑line arguments, loads a DWG file, selects a layout, and calls the rendering pipeline.

## Key C# concepts shown
* **`static class` / `static void Main`** – the executable entry point.
* **Nullable reference type** – `string? debugFilePath`.
* **`for` loop** for argument parsing.
* **String interpolation** – `$"{variable}"`.
* **`if` statements** and **`else if`** chain.
* **`foreach`** iteration over files.
* **`using` statements** at the top import namespaces.
* **Pattern matching** – `if (entity is Viewport vp)` inside layout selection.
* **`?.` null‑conditional operator** (e.g., `layout?.Name`).
* **`Path.Combine`** for building file paths.

## Simplified flow (pseudocode)
```
Parse args → set defaults
If input is a directory
    foreach dwg file
        Load DWG → select layout → render → optional debug report
Else (single file)
    Load DWG → select layout → render → optional debug report
```

The file also integrates **`PerformanceTracker`** (optional profiling) and **`DebugReportGenerator`** (debug output).
