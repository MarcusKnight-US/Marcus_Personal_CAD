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
        string inputPath  = @"C:\Users\BRG\Documents\blocks_and_tables_-_metric.dwg";
        string outputPath = @"C:\Users\BRG\Documents\output.png";
        string bgColor    = "#FFFFFF";     // white background for layout renders
        bool   benchmark  = false;
        int    benchIter  = 5;

        // ── Argument parsing ─────────────────────────────────────────────────
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if      (arg.StartsWith("--bg=",        StringComparison.OrdinalIgnoreCase)) bgColor   = arg[5..];
            else if (arg.Equals("--bg",             StringComparison.OrdinalIgnoreCase)) bgColor   = args[++i];
            else if (arg.Equals("--benchmark",      StringComparison.OrdinalIgnoreCase)) benchmark = true;
            else if (arg.StartsWith("--iterations=",StringComparison.OrdinalIgnoreCase)) int.TryParse(arg[13..], out benchIter);
            else if (i == 0 && !arg.StartsWith("-")) inputPath  = arg;
            else if (i == 1 && !arg.StartsWith("-")) outputPath = arg;
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"ERROR: Input file not found: {inputPath}");
            Environment.Exit(1);
        }

        // ── Layout detection ─────────────────────────────────────────────────
        var doc = DwgReader.Read(inputPath, null);
        var layout = doc.Layouts
            .OrderByDescending(l => l.AssociatedBlock?.Entities?.Count ?? 0)   // prefer the richest layout
            .FirstOrDefault(l => !l.Name.Equals("Model", StringComparison.OrdinalIgnoreCase));

        if (layout != null)
            Console.WriteLine($"Layout : '{layout.Name}'");
        else
            Console.WriteLine("No paper-space layout found — rendering Model space.");

        // ── Normal single-pass render ─────────────────────────────────────────
        if (!benchmark)
        {
            Render(doc, layout, inputPath, outputPath, bgColor);
            Console.WriteLine($"Saved  : {outputPath}");
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
            var iterLayout = iterDoc.Layouts
                .OrderByDescending(l => l.AssociatedBlock?.Entities?.Count ?? 0)
                .FirstOrDefault(l => !l.Name.Equals("Model", StringComparison.OrdinalIgnoreCase));

            Render(iterDoc, iterLayout, inputPath, outputPath, bgColor);

            sw.Stop();
            totalMs += sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"  Iter {iter}: {sw.Elapsed.TotalMilliseconds:F1} ms");
        }

        Console.WriteLine($"\nAverage: {totalMs / benchIter:F1} ms over {benchIter} iterations");
        Console.WriteLine(DwgToPngConverter.PerformanceTracker.GetReport());
    }

    // ── Shared render helper ──────────────────────────────────────────────────
    private static void Render(CadDocument doc, ACadSharp.Objects.Layout? layout,
        string inputPath, string outputPath, string bgColor)
    {
        var renderer = new MasterRenderer();
        renderer.BackgroundColorHex = bgColor;

        if (layout != null)
        {
            renderer.RenderLayout(doc, layout, outputPath, inputPath);
        }
        else
        {
            var reader = new CadDwgReader();
            var scene  = new CadScene();
            scene.AddEntities(reader.ReadAll(doc));
            renderer.RenderAll(scene.Entities, scene.BoundingBox, outputPath, inputPath);
        }
    }
}
