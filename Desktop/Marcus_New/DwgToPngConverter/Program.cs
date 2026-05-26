using System;
using System.IO;
using System.Text;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using DwgToPngConverter.Readers;
using DwgToPngConverter.Renderers;
using DwgToPngConverter.Scene;

class Program
{
    static void Main(string[] args)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        string inputPath = @"C:\Users\BRG\Documents\Drawing2.dwg";
        string outputPath = @"C:\Users\BRG\Documents\output.png";
        string debugPath = @"C:\Users\BRG\Documents\dwg_debug.txt";
        string bgColor = "#FFFFFF";

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

            // Read the DWG document using ACadSharp directly for debugging
            var doc = DwgReader.Read(inputPath, null);
            sb.AppendLine($"DWG loaded successfully.");
            sb.AppendLine($"Document Entities Count in Model Space: {doc.Entities.Count}");

            sb.AppendLine($"Document Block Records Count: {doc.BlockRecords.Count}");
            foreach (var br in doc.BlockRecords)
            {
                sb.AppendLine($"  - BlockRecord: Name={br.Name}, Entities Count={br.Entities.Count}");
                foreach (var e in br.Entities)
                {
                    sb.AppendLine($"    * Entity: Type={e.GetType().Name}, Handle={e.Handle}");
                }
            }

            // Now run the actual converter
            var reader = new CadDwgReader();
            var scene = new CadScene();
            scene.AddEntities(reader.ReadAll(inputPath));

            var bbox = scene.BoundingBox;
            sb.AppendLine($"BoundingBox: MinX={bbox.MinX}, MinY={bbox.MinY}, MaxX={bbox.MaxX}, MaxY={bbox.MaxY}");
            sb.AppendLine($"Total Processed Entities in Scene: {scene.Entities.Count}");

            foreach (var sEntity in scene.Entities)
            {
                if (sEntity is Line sLine)
                {
                    sb.AppendLine($"  * Scene Entity: Type=Line, Handle={sLine.Handle}, Start=({sLine.StartPoint.X}, {sLine.StartPoint.Y}), End=({sLine.EndPoint.X}, {sLine.EndPoint.Y})");
                }
                else
                {
                    sb.AppendLine($"  * Scene Entity: Type={sEntity.GetType().Name}, Handle={sEntity.Handle}");
                }
            }

            var renderer = new MasterRenderer();
            renderer.BackgroundColorHex = bgColor;
            renderer.RenderAll(scene.Entities, scene.BoundingBox, outputPath, inputPath);
            sb.AppendLine($"Image rendered and saved to: {outputPath}");

            stopwatch.Stop();
            sb.AppendLine($"Execution Time: {stopwatch.Elapsed.TotalSeconds:F3} seconds ({stopwatch.ElapsedMilliseconds} ms)");

            Console.WriteLine($"Processing finished successfully in {stopwatch.Elapsed.TotalSeconds:F3} seconds ({stopwatch.ElapsedMilliseconds} ms). Debug info written to " + debugPath);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            sb.AppendLine($"ERROR EXCEPTION: {ex.Message}");
            sb.AppendLine(ex.StackTrace);
            Console.WriteLine($"ERROR after {stopwatch.Elapsed.TotalSeconds:F3} seconds: {ex.Message}");
        }
        finally
        {
            File.WriteAllText(debugPath, sb.ToString());
        }
    }
}
