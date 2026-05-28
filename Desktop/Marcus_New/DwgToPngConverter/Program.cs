using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using DwgToPngConverter.Readers;
using DwgToPngConverter.Renderers;
using DwgToPngConverter.Scene;
using DwgToPngConverter.Geometry;
using ACadSharp.Objects;

namespace DwgToPngConverter
{
    public static class PerformanceTracker
    {
        public static readonly ConcurrentDictionary<string, double> ExtentsTime = new();
        public static readonly ConcurrentDictionary<string, int> ExtentsCount = new();
        public static readonly ConcurrentDictionary<string, double> RenderTime = new();
        public static readonly ConcurrentDictionary<string, int> RenderCount = new();

        public static void Reset()
        {
            ExtentsTime.Clear();
            ExtentsCount.Clear();
            RenderTime.Clear();
            RenderCount.Clear();
        }

        public static void RecordExtents(string typeName, double elapsedMs)
        {
            ExtentsTime.AddOrUpdate(typeName, elapsedMs, (k, old) => old + elapsedMs);
            ExtentsCount.AddOrUpdate(typeName, 1, (k, old) => old + 1);
        }

        public static void RecordRender(string typeName, double elapsedMs)
        {
            RenderTime.AddOrUpdate(typeName, elapsedMs, (k, old) => old + elapsedMs);
            RenderCount.AddOrUpdate(typeName, 1, (k, old) => old + 1);
        }

        public static string GetReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("=========================================================================");
            sb.AppendLine("                     DETAILED PERFORMANCE PROFILE                        ");
            sb.AppendLine("=========================================================================");

            sb.AppendLine("\n--- 1. Bounding Box & Extents Calculations (by Entity Type) ---");
            if (ExtentsTime.IsEmpty)
            {
                sb.AppendLine("No extents calculations recorded.");
            }
            else
            {
                sb.AppendLine($"{"Entity Type",-25} | {"Count",-8} | {"Total Time (ms)",-18} | {"Avg Time (ms)",-15}");
                sb.AppendLine(new string('-', 72));
                var sortedExtents = ExtentsTime.Keys.OrderByDescending(k => ExtentsTime[k]);
                double totalExtentsMs = 0;
                int totalExtentsCount = 0;
                foreach (var key in sortedExtents)
                {
                    double time = ExtentsTime[key];
                    int count = ExtentsCount[key];
                    totalExtentsMs += time;
                    totalExtentsCount += count;
                    sb.AppendLine($"{key,-25} | {count,-8} | {time,18:F3} | {time / count,15:F3}");
                }
                sb.AppendLine(new string('-', 72));
                sb.AppendLine($"{"TOTAL",-25} | {totalExtentsCount,-8} | {totalExtentsMs,18:F3} | {totalExtentsMs / Math.Max(1, totalExtentsCount),15:F3}");
            }

            sb.AppendLine("\n--- 2. Entity Rendering & Drawing (by Entity Type) ---");
            if (RenderTime.IsEmpty)
            {
                sb.AppendLine("No rendering recorded.");
            }
            else
            {
                sb.AppendLine($"{"Entity Type",-25} | {"Count",-8} | {"Total Time (ms)",-18} | {"Avg Time (ms)",-15}");
                sb.AppendLine(new string('-', 72));
                var sortedRender = RenderTime.Keys.OrderByDescending(k => RenderTime[k]);
                double totalRenderMs = 0;
                int totalRenderCount = 0;
                foreach (var key in sortedRender)
                {
                    double time = RenderTime[key];
                    int count = RenderCount[key];
                    totalRenderMs += time;
                    totalRenderCount += count;
                    sb.AppendLine($"{key,-25} | {count,-8} | {time,18:F3} | {time / count,15:F3}");
                }
                sb.AppendLine(new string('-', 72));
                sb.AppendLine($"{"TOTAL",-25} | {totalRenderCount,-8} | {totalRenderMs,18:F3} | {totalRenderMs / Math.Max(1, totalRenderCount),15:F3}");
            }
            sb.AppendLine("=========================================================================");
            return sb.ToString();
        }
    }
}

class Program
{
    static void Main(string[] args)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        string inputPath = @"C:\Users\BRG\Documents\blocks_and_tables_-_metric.dwg";
        string outputPath = @"C:\Users\BRG\Documents\output.png";
        string debugPath = @"C:\Users\BRG\Documents\dwg_debug.txt";
        string bgColor = "#000000"; //Black

        if (args.Length > 0)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--bg=", StringComparison.OrdinalIgnoreCase))
                {
                    bgColor = args[i].Substring(5);
                }
                else if (args[i].Equals("--bg", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    bgColor = args[++i];
                }
                else if (i == 0 && !args[i].StartsWith("-"))
                {
                    inputPath = args[i];
                }
                else if (i == 1 && !args[i].StartsWith("-"))
                {
                    outputPath = args[i];
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== DWG Debug Report ===");
        sb.AppendLine($"Local Time: {DateTime.Now}");
        sb.AppendLine($"Input File: {inputPath}");
        sb.AppendLine($"Output File: {outputPath}");
        sb.AppendLine($"Background Color: {bgColor}");

        try
        {
            if (!File.Exists(inputPath))
            {
                sb.AppendLine($"ERROR: Input file does not exist!");
                File.WriteAllText(debugPath, sb.ToString());
                Console.WriteLine("Input file not found.");
                return;
            }

            string selectedLayoutName = null;
            var docForDetection = DwgReader.Read(inputPath, null);
            var detectedLayout = docForDetection.Layouts.FirstOrDefault(l => l.Name.Equals("SECTIONS AND DETAILS", StringComparison.OrdinalIgnoreCase))
                         ?? docForDetection.Layouts.FirstOrDefault(l => !l.Name.Equals("Model", StringComparison.OrdinalIgnoreCase));
            selectedLayoutName = detectedLayout?.Name;

            if (selectedLayoutName != null)
            {
                Console.WriteLine($"Detected paper space layout: '{selectedLayoutName}'");
            }
            else
            {
                Console.WriteLine("No paper space layout found, defaulting to Model space rendering.");
            }

            Console.WriteLine("Starting performance runs and profiling...");

            // --- 1. WARMUP RUN (JIT Compilation) ---
            Console.WriteLine("Running warmup to JIT compile the code...");
            {
                var doc = DwgReader.Read(inputPath, null);
                var reader = new CadDwgReader();
                var scene = new CadScene();
                var rawEntities = reader.ReadAll(doc);
                scene.AddEntities(rawEntities);

                Console.WriteLine("--- DIAGNOSTIC TEXT SCAN ---");
                int textWithFormattingCount = 0;
                foreach (var ent in scene.Entities)
                {
                    if (ent is TextEntity te && (te.Value.Contains("\\") || te.Value.Contains("\n") || te.Value.Contains("\r")))
                    {
                        Console.WriteLine($"TextEntity (Handle={te.Handle}, Layer={te.Layer?.Name}): '{te.Value}'");
                        textWithFormattingCount++;
                    }
                    else if (ent is MText mt && mt.Value.Contains("GENERAL NOTES"))
                    {
                        Console.WriteLine($"MText GENERAL NOTES: RectangleWidth={mt.RectangleWidth}, Height={mt.Height}, Layer={mt.Layer?.Name}");
                    }
                }
                Console.WriteLine("-----------------------------");


                scene = new CadScene();
                scene.AddEntities(reader.ReadAll(doc));
                Console.WriteLine($"Exploded Model Space Bounding Box: MinX={scene.BoundingBox.MinX:F3}, MaxX={scene.BoundingBox.MaxX:F3}, MinY={scene.BoundingBox.MinY:F3}, MaxY={scene.BoundingBox.MaxY:F3}");

                var renderer = new MasterRenderer();
                if (selectedLayoutName != null)
                {
                    var lObj = doc.Layouts.FirstOrDefault(l => l.Name.Equals(selectedLayoutName));
                    if (lObj != null && lObj.AssociatedBlock != null)
                    {
                        var paperScene = new CadScene();
                        paperScene.AddEntities(lObj.AssociatedBlock.Entities);
                        Console.WriteLine("--- DIAGNOSTIC PAPER SPACE TEXT SCAN ---");
                        int paperFormattingCount = 0;
                        foreach (var ent in paperScene.Entities)
                        {
                            if (ent is TextEntity te && (te.Value.Contains("\\") || te.Value.Contains("\n") || te.Value.Contains("\r")))
                            {
                                Console.WriteLine($"Paper TextEntity (Handle={te.Handle}, Layer={te.Layer?.Name}): '{te.Value}'");
                                paperFormattingCount++;
                            }
                            else if (ent is MText mt && (mt.Value.Contains("\\") || mt.Value.Contains("\n") || mt.Value.Contains("\r")))
                            {
                                Console.WriteLine($"Paper MText (Handle={mt.Handle}, Layer={mt.Layer?.Name}, RectangleWidth={mt.RectangleWidth}): '{mt.Value}'");
                                paperFormattingCount++;
                            }
                        }
                        Console.WriteLine($"Found {paperFormattingCount} Paper Space text entities with formatting/newlines.");
                        Console.WriteLine("-----------------------------------------");
                    }
                    renderer.RenderLayout(doc, lObj, outputPath, inputPath);
                }
                else
                {
                    reader = new CadDwgReader();
                    scene = new CadScene();
                    scene.AddEntities(reader.ReadAll(doc));
                    renderer.BackgroundColorHex = bgColor;
                    renderer.RenderAll(scene.Entities, scene.BoundingBox, outputPath, inputPath);
                }
            }
            DwgToPngConverter.PerformanceTracker.Reset();

            // --- 2. MEASURED RUNS ---
            int iterations = 5;
            Console.WriteLine($"Running {iterations} benchmark iterations...");

            double totalLoad1Ms = 0;
            double totalLoad2Ms = 0;
            double totalScenePrepMs = 0;
            double totalRenderMs = 0;

            for (int iter = 1; iter <= iterations; iter++)
            {
                var iterSw = Stopwatch.StartNew();

                // Measure Phase 1: Debug DwgReader.Read
                var sw = Stopwatch.StartNew();
                var doc = DwgReader.Read(inputPath, null);
                sw.Stop();
                double load1Ms = sw.Elapsed.TotalMilliseconds;
                totalLoad1Ms += load1Ms;

                // Measure Phase 2: CadDwgReader.ReadAll
                sw = Stopwatch.StartNew();
                var reader = new CadDwgReader();
                var rawEntities = reader.ReadAll(doc);
                sw.Stop();
                double load2Ms = sw.Elapsed.TotalMilliseconds;
                totalLoad2Ms += load2Ms;

                // Measure Phase 3: CadScene preparation & exploding
                sw = Stopwatch.StartNew();
                var scene = new CadScene();
                scene.AddEntities(rawEntities);
                sw.Stop();
                double scenePrepMs = sw.Elapsed.TotalMilliseconds;
                totalScenePrepMs += scenePrepMs;

                // Measure Phase 4: MasterRenderer drawing & saving
                sw = Stopwatch.StartNew();
                var renderer = new MasterRenderer();
                if (selectedLayoutName != null)
                {
                    var layout = doc.Layouts.FirstOrDefault(l => l.Name.Equals(selectedLayoutName));
                    renderer.RenderLayout(doc, layout, outputPath, inputPath);
                }
                else
                {
                    renderer.BackgroundColorHex = bgColor;
                    renderer.RenderAll(scene.Entities, scene.BoundingBox, outputPath, inputPath);
                }
                sw.Stop();
                double renderMs = sw.Elapsed.TotalMilliseconds;
                totalRenderMs += renderMs;

                iterSw.Stop();
                Console.WriteLine($"  Iteration {iter}: {iterSw.Elapsed.TotalMilliseconds:F1} ms");

                // In the first measured iteration, record general debug info for the report
                if (iter == 1)
                {
                    sb.AppendLine($"DWG loaded successfully.");
                    sb.AppendLine($"Selected Layout: {selectedLayoutName ?? "Model Space"}");
                    sb.AppendLine($"Document Entities Count in Model Space: {doc.Entities.Count}");
                    sb.AppendLine($"Document Block Records Count: {doc.BlockRecords.Count}");

                    if (selectedLayoutName != null)
                    {
                        var layout = doc.Layouts.FirstOrDefault(l => l.Name.Equals(selectedLayoutName));
                        if (layout != null && layout.AssociatedBlock != null)
                        {
                            sb.AppendLine($"Layout Entities Count: {layout.AssociatedBlock.Entities.Count}");
                        }
                    }

                    var bbox = selectedLayoutName != null ? new BoundingBox() : scene.BoundingBox;
                    if (selectedLayoutName != null)
                    {
                        var layout = doc.Layouts.FirstOrDefault(l => l.Name.Equals(selectedLayoutName));
                        var paperScene = new DwgToPngConverter.Scene.CadScene();
                        paperScene.AddEntities(layout.AssociatedBlock.Entities);
                        var mainVp = paperScene.Entities.OfType<Viewport>().FirstOrDefault(v => v.Id == 1);
                        if (mainVp != null)
                        {
                            bbox.MinX = mainVp.Center.X - mainVp.Width / 2;
                            bbox.MaxX = mainVp.Center.X + mainVp.Width / 2;
                            bbox.MinY = mainVp.Center.Y - mainVp.Height / 2;
                            bbox.MaxY = mainVp.Center.Y + mainVp.Height / 2;
                        }
                        else
                        {
                            bbox = paperScene.BoundingBox;
                        }
                    }

                    sb.AppendLine($"BoundingBox: MinX={bbox.MinX}, MinY={bbox.MinY}, MaxX={bbox.MaxX}, MaxY={bbox.MaxY}");
                    sb.AppendLine($"Image rendered and saved to: {outputPath}");
                }
            }

            double avgLoad1 = totalLoad1Ms / iterations;
            double avgLoad2 = totalLoad2Ms / iterations;
            double avgScenePrep = totalScenePrepMs / iterations;
            double avgRender = totalRenderMs / iterations;
            double avgTotal = avgLoad1 + avgLoad2 + avgScenePrep + avgRender;

            // Average detailed entity performance stats by dividing by iterations
            var report = DwgToPngConverter.PerformanceTracker.GetReport();

            Console.WriteLine("\n=========================================================================");
            Console.WriteLine("                         BENCHMARK RESULTS                               ");
            Console.WriteLine("=========================================================================");
            Console.WriteLine($"1. Debug Load (DwgReader.Read):      {avgLoad1,10:F2} ms");
            Console.WriteLine($"2. Actual Load (reader.ReadAll):      {avgLoad2,10:F2} ms");
            Console.WriteLine($"3. Scene Preparation & Explode:       {avgScenePrep,10:F2} ms");
            Console.WriteLine($"4. MasterRenderer.RenderAll & Save:   {avgRender,10:F2} ms");
            Console.WriteLine(new string('-', 72));
            Console.WriteLine($"AVERAGE TOTAL PIPELINE TIME:          {avgTotal,10:F2} ms");
            Console.WriteLine("=========================================================================");

            Console.WriteLine(report);

            sb.AppendLine("\n=== BENCHMARK PIPELINE STATS ===");
            sb.AppendLine($"Average Debug Load (DwgReader.Read):      {avgLoad1:F2} ms");
            sb.AppendLine($"Average Actual Load (reader.ReadAll):      {avgLoad2:F2} ms");
            sb.AppendLine($"Average Scene Preparation & Explode:       {avgScenePrep:F2} ms");
            sb.AppendLine($"Average MasterRenderer.RenderAll & Save:   {avgRender:F2} ms");
            sb.AppendLine($"AVERAGE TOTAL PIPELINE TIME:               {avgTotal:F2} ms");
            sb.AppendLine(report);

            stopwatch.Stop();
            Console.WriteLine($"\nProcessing finished successfully. Detailed profiling printed and debug info written to " + debugPath);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            sb.AppendLine($"ERROR EXCEPTION: {ex.Message}");
            sb.AppendLine(ex.StackTrace);
            Console.WriteLine($"ERROR: {ex.Message}");
        }
        finally
        {
            File.WriteAllText(debugPath, sb.ToString());
        }
    }
}
