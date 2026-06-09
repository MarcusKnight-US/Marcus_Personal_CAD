using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using DwgToPngConverter.Geometry;
using DwgToPngConverter.Readers;
using DwgToPngConverter.Renderers;
using DwgToPngConverter.Scene;
using DwgToPngConverter;

// ─────────────────────────────────────────────────────────────────────────────
//  PerformanceTracker – optional profiling harness, disabled in normal mode
// ─────────────────────────────────────────────────────────────────────────────
namespace DwgToPngConverter
{
    public static class PerformanceTracker
    {
        public static bool Enabled { get; set; } = false;

        public static readonly ConcurrentDictionary<string, double> ExtentsTime = new();
        public static readonly ConcurrentDictionary<string, int>    ExtentsCount = new();
        public static readonly ConcurrentDictionary<string, double> RenderTime  = new();
        public static readonly ConcurrentDictionary<string, int>    RenderCount = new();

        public static void Reset()
        {
            ExtentsTime.Clear();
            ExtentsCount.Clear();
            RenderTime.Clear();
            RenderCount.Clear();
        }

        public static void RecordExtents(string typeName, double elapsedMs)
        {
            if (!Enabled) return;
            ExtentsTime.AddOrUpdate(typeName, elapsedMs, (_, old) => old + elapsedMs);
            ExtentsCount.AddOrUpdate(typeName, 1, (_, old) => old + 1);
        }

        public static void RecordRender(string typeName, double elapsedMs)
        {
            if (!Enabled) return;
            RenderTime.AddOrUpdate(typeName, elapsedMs, (_, old) => old + elapsedMs);
            RenderCount.AddOrUpdate(typeName, 1, (_, old) => old + 1);
        }

        public static string GetReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("=========================================================================");
            sb.AppendLine("                     DETAILED PERFORMANCE PROFILE                        ");
            sb.AppendLine("=========================================================================");

            AppendTable(sb, "Bounding Box & Extents Calculations", ExtentsTime, ExtentsCount);
            AppendTable(sb, "Entity Rendering & Drawing",           RenderTime,  RenderCount);

            sb.AppendLine("=========================================================================");
            return sb.ToString();
        }

        private static void AppendTable(StringBuilder sb, string title,
            ConcurrentDictionary<string, double> timeMap,
            ConcurrentDictionary<string, int>    countMap)
        {
            sb.AppendLine($"\n--- {title} (by Entity Type) ---");
            if (timeMap.IsEmpty) { sb.AppendLine("  (none recorded)"); return; }

            sb.AppendLine($"{"Entity Type",-25} | {"Count",-8} | {"Total Time (ms)",-18} | {"Avg Time (ms)",-15}");
            sb.AppendLine(new string('-', 72));

            double totalMs = 0; int totalCount = 0;
            foreach (var key in timeMap.Keys.OrderByDescending(k => timeMap[k]))
            {
                double t = timeMap[key]; int c = countMap[key];
                totalMs += t; totalCount += c;
                sb.AppendLine($"{key,-25} | {c,-8} | {t,18:F3} | {t / c,15:F3}");
            }
            sb.AppendLine(new string('-', 72));
            sb.AppendLine($"{"TOTAL",-25} | {totalCount,-8} | {totalMs,18:F3} | {totalMs / Math.Max(1, totalCount),15:F3}");
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Entry point
// ─────────────────────────────────────────────────────────────────────────────
class Program
{
    static void Main(string[] args)
    {
        // ── Defaults ──────────────────────────────────────────────────────────
        string inputPath      = "dwg_examples";
        string outputPath     = "dwg_output";
        string bgColor        = "#FFFFFF";     // white background for layout renders
        bool   benchmark      = false;
        int    benchIter      = 5;
        bool   enableDebug    = false;
        string? debugFilePath = null;

        // ── Argument parsing ─────────────────────────────────────────────────
        int positionalIndex = 0;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if      (arg.StartsWith("--bg=",         StringComparison.OrdinalIgnoreCase)) bgColor   = arg[5..];
            else if (arg.Equals("--bg",              StringComparison.OrdinalIgnoreCase)) bgColor   = args[++i];
            else if (arg.Equals("--benchmark",       StringComparison.OrdinalIgnoreCase)) benchmark = true;
            else if (arg.StartsWith("--iterations=", StringComparison.OrdinalIgnoreCase)) int.TryParse(arg[13..], out benchIter);
            else if (arg.Equals("--debug",           StringComparison.OrdinalIgnoreCase)) enableDebug = true;
            else if (arg.StartsWith("--debug-file=", StringComparison.OrdinalIgnoreCase)) { debugFilePath = arg[13..]; enableDebug = true; }
            else if (arg.Equals("--debug-file",      StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    debugFilePath = args[++i];
                    enableDebug = true;
                }
            }
            else if (!arg.StartsWith("-"))
            {
                if (positionalIndex == 0)
                {
                    inputPath = arg;
                    positionalIndex++;
                }
                else if (positionalIndex == 1)
                {
                    outputPath = arg;
                    positionalIndex++;
                }
            }
        }

        if (Directory.Exists(inputPath))
        {
            var dwgFiles = Directory.GetFiles(inputPath, "*.dwg", SearchOption.TopDirectoryOnly);
            if (dwgFiles.Length == 0)
            {
                Console.Error.WriteLine($"ERROR: No DWG files found in directory: {inputPath}");
                Environment.Exit(1);
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            Console.WriteLine($"Found {dwgFiles.Length} DWG files to convert in directory: {inputPath}");
            foreach (var file in dwgFiles)
            {
                string filename = Path.GetFileName(file);
                string pngName = Path.GetFileNameWithoutExtension(file) + ".png";
                string outPath = Path.Combine(outputPath, pngName);

                Console.WriteLine("\n--------------------------------------------------");
                Console.WriteLine($"Converting: {filename}");
                Console.WriteLine($"To: {outPath}");

                var dirOverallTimer = System.Diagnostics.Stopwatch.StartNew();
                var dirDebugInfo = new ConversionDebugInfo();
                if (enableDebug)
                {
                    dirDebugInfo.DwgPath = Path.GetFullPath(file);
                    dirDebugInfo.PngPath = Path.GetFullPath(outPath);
                    if (File.Exists(file))
                    {
                        dirDebugInfo.DwgSize = new FileInfo(file).Length;
                    }
                    DwgToPngConverter.PerformanceTracker.Enabled = true;
                    DwgToPngConverter.PerformanceTracker.Reset();
                }

                try
                {
                    var dirLoadSw = System.Diagnostics.Stopwatch.StartNew();
                    var dirDoc = DwgReader.Read(file, null);
                    dirLoadSw.Stop();
                    if (enableDebug) dirDebugInfo.LoadTimeMs = dirLoadSw.Elapsed.TotalMilliseconds;

                    var dirLayoutSw = System.Diagnostics.Stopwatch.StartNew();
                    var dirLayout = SelectLayout(dirDoc);
                    dirLayoutSw.Stop();
                    if (enableDebug) dirDebugInfo.LayoutSelectTimeMs = dirLayoutSw.Elapsed.TotalMilliseconds;

                    if (dirLayout != null)
                        Console.WriteLine($"Layout : '{dirLayout.Name}'");
                    else
                        Console.WriteLine("No paper-space layout found — rendering Model space.");

                    var dirRenderSw = System.Diagnostics.Stopwatch.StartNew();
                    Render(dirDoc, dirLayout, file, outPath, bgColor, enableDebug ? dirDebugInfo : null);
                    dirRenderSw.Stop();
                    if (enableDebug) dirDebugInfo.RenderTimeMs = dirRenderSw.Elapsed.TotalMilliseconds;

                    Console.WriteLine($"Saved  : {outPath}");

                    if (enableDebug)
                    {
                        string targetReportPath = debugFilePath != null
                            ? (Path.Combine(Path.GetDirectoryName(debugFilePath) ?? "", Path.GetFileNameWithoutExtension(debugFilePath) + "_" + Path.GetFileNameWithoutExtension(file) + Path.GetExtension(debugFilePath)))
                            : (outPath + ".debug.txt");
                        DebugReportGenerator.GenerateReport(targetReportPath, dirDebugInfo, dirOverallTimer);
                        Console.WriteLine($"Saved Debug Report: {targetReportPath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"FAILED to convert {filename}: {ex.Message}");
                }
            }
            return;
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"ERROR: Input path not found (file or directory): {inputPath}");
            Environment.Exit(1);
        }

        var overallTimer = System.Diagnostics.Stopwatch.StartNew();
        var debugInfo = new ConversionDebugInfo();
        if (enableDebug)
        {
            debugInfo.DwgPath = Path.GetFullPath(inputPath);
            debugInfo.PngPath = Path.GetFullPath(outputPath);
            if (File.Exists(inputPath))
            {
                debugInfo.DwgSize = new FileInfo(inputPath).Length;
            }
            DwgToPngConverter.PerformanceTracker.Enabled = true;
            DwgToPngConverter.PerformanceTracker.Reset();
        }

        // ── Layout detection ─────────────────────────────────────────────────
        var loadSw = System.Diagnostics.Stopwatch.StartNew();
        var doc = DwgReader.Read(inputPath, null);
        loadSw.Stop();
        if (enableDebug) debugInfo.LoadTimeMs = loadSw.Elapsed.TotalMilliseconds;

        var layoutSw = System.Diagnostics.Stopwatch.StartNew();
        var layout = SelectLayout(doc);
        layoutSw.Stop();
        if (enableDebug) debugInfo.LayoutSelectTimeMs = layoutSw.Elapsed.TotalMilliseconds;

        if (layout != null)
            Console.WriteLine($"Layout : '{layout.Name}'");
        else
            Console.WriteLine("No paper-space layout found — rendering Model space.");

        // ── Normal single-pass render ─────────────────────────────────────────
        if (!benchmark)
        {
            var renderSw = System.Diagnostics.Stopwatch.StartNew();
            Render(doc, layout, inputPath, outputPath, bgColor, enableDebug ? debugInfo : null);
            renderSw.Stop();
            if (enableDebug) debugInfo.RenderTimeMs = renderSw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"Saved  : {outputPath}");

            if (enableDebug)
            {
                string targetReportPath = debugFilePath ?? (outputPath + ".debug.txt");
                DebugReportGenerator.GenerateReport(targetReportPath, debugInfo, overallTimer);
                Console.WriteLine($"Saved Debug Report: {targetReportPath}");
            }
            return;
        }

        // ── Benchmark mode (--benchmark) ─────────────────────────────────────
        DwgToPngConverter.PerformanceTracker.Enabled = true;
        Console.WriteLine($"Benchmarking {benchIter} iterations…");

        double totalMs = 0;
        for (int iter = 1; iter <= benchIter; iter++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Re-read each time so load time is measured too
            var iterDoc = DwgReader.Read(inputPath, null);
            var iterLayout = SelectLayout(iterDoc);

            Render(iterDoc, iterLayout, inputPath, outputPath, bgColor);

            sw.Stop();
            totalMs += sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"  Iter {iter}: {sw.Elapsed.TotalMilliseconds:F1} ms");
        }

        Console.WriteLine($"\nAverage: {totalMs / benchIter:F1} ms over {benchIter} iterations");
        Console.WriteLine(DwgToPngConverter.PerformanceTracker.GetReport());
    }

    private static ACadSharp.Objects.Layout? SelectLayout(CadDocument doc)
    {
        var layout = doc.Layouts
            .OrderByDescending(l => l.AssociatedBlock?.Entities?.Count ?? 0)
            .FirstOrDefault(l => !l.Name.Equals("Model", StringComparison.OrdinalIgnoreCase));

        if (layout != null)
        {
            bool hasContent = false;
            if (layout.AssociatedBlock != null)
            {
                foreach (var entity in layout.AssociatedBlock.Entities)
                {
                    if (entity == null) continue;
                    if (entity is Viewport vp)
                    {
                        if (vp.Id > 1)
                        {
                            hasContent = true;
                            break;
                        }
                    }
                    else
                    {
                        hasContent = true;
                        break;
                    }
                }
            }
            if (!hasContent)
            {
                return null;
            }
        }
        return layout;
    }

    // ── Shared render helper ──────────────────────────────────────────────────
    private static void Render(CadDocument doc, ACadSharp.Objects.Layout? layout,
        string inputPath, string outputPath, string bgColor, ConversionDebugInfo? debugInfo = null)
    {
        var renderer = new MasterRenderer();
        renderer.BackgroundColorHex = bgColor;

        if (layout != null)
        {
            renderer.RenderLayout(doc, layout, outputPath, inputPath, debugInfo);
        }
        else
        {
            var reader = new CadDwgReader();
            var scene  = new CadScene();
            scene.AddEntities(reader.ReadAll(doc));
            renderer.RenderAll(scene.Entities, scene.BoundingBox, outputPath, inputPath, debugInfo);
        }
    }
}
