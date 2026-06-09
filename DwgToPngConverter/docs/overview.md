# Overview of DWGtoPNG Project

## What the project does
The **DWGtoPNG** tool converts AutoCAD DWG/DXF files into PNG images.  It reads a DWG file, selects an appropriate layout (paper‑space or model‑space), builds a scene representation, and renders the geometry to a bitmap.

## High‑level flow (as described in `Program.cs`)
1. **Parse command‑line arguments** – set input folder, output folder, background colour, debug options, etc.
2. **Load the DWG file** using `DwgReader.Read` (implemented in `Readers/CadDwgReader.cs`).
3. **Select a layout** with `SelectLayout` – prefers a populated paper‑space layout.
4. **Create a `MasterRenderer`** and either:
   - Render a specific layout (`RenderLayout`) **or**
   - Render the whole model space (`RenderAll`).
5. **Optionally** generate a debug report (`DebugReportGenerator.cs`).
6. **Optionally** record performance statistics (`PerformanceTracker` static class).

## Main components
| Folder | Key purpose | Example files |
|--------|-------------|---------------|
| `Geometry` | Geometry helpers (bounding boxes, transformations) | `BoundingBox.cs`, `ExtentsCalculator.cs` |
| `Readers` | DWG file parsing – wraps **ACadSharp** library | `CadDwgReader.cs` |
| `Renderers` | Rendering individual entity types and orchestrating the full image | `MasterRenderer.cs`, `LineRenderer.cs`, `HatchRenderer.cs` |
| `Scene` | Holds a collection of entities and a bounding box for the whole drawing | `CadScene.cs` |
| Root | Entry point, configuration, debugging, performance tracking | `Program.cs`, `AppConfig.cs`, `DebugReportGenerator.cs` |

## How the pieces fit together
```text
Program.cs → (args) → CadDwgReader.cs → CadDocument
               │                        │
               └─ SelectLayout() ──────► Layout?
               │                        │
               └─ MasterRenderer ──────► Renderers (per entity)
               │                        │
               └─ DebugReportGenerator (optional) │
               │                        └─ PerformanceTracker (optional)
```
*The arrows show data flow; each component consumes the output of the previous step.*

## C# syntax highlights you’ll see
- **`using` statements** – import namespaces.
- **`static class`** – a class that only contains static members (`Program`, `PerformanceTracker`).
- **Nullable reference types** (`string? debugFilePath`).
- **`foreach` loops**, **`if` statements**, **ternary operator** (`condition ? a : b`).
- **String interpolation** – `$"{variable}"`.
- **Expression‑bodied members** – `public static bool Enabled { get; set; } = false;`.
- **Pattern matching** – `if (entity is Viewport vp)`.

---
*All other Markdown files follow the same structure: a short purpose, key members, and brief syntax notes.*
