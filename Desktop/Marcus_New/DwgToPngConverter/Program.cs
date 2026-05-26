using System;
using DwgToPngConverter.Readers;
using DwgToPngConverter.Renderers;
using DwgToPngConverter.Scene;
using ACadSharp.Entities;

// Program is the main entry point for the DWG to PNG converter.
class Program
{
    static void Main(string[] args)
    {
        // Start the converter and show a simple status message.
        Console.WriteLine("DWG to PNG Converter Starting...");

        try
        {
            // Input DWG path and output PNG path are hardcoded for now.
            string inputPath = @"C:\Users\BRG\Documents\Drawing2.dwg";
            string outputPath = @"C:\Users\BRG\Documents\output.png";

            Console.WriteLine("Loading:"+@"C:\Users\BRG\Documents\Drawing2.dwg");

            var reader = new CadDwgReader();
            var scene = new CadScene();
            scene.AddEntities(reader.ReadAll(inputPath));

            // Output bounding box extents for verification of joined geometry.
            var bbox = scene.BoundingBox;
            Console.WriteLine($"BoundingBox: MinX={bbox.MinX}, MinY={bbox.MinY}, MaxX={bbox.MaxX}, MaxY={bbox.MaxY}");

            var renderer = new MasterRenderer();
            renderer.RenderAll(scene.Entities, scene.BoundingBox, outputPath);

            Console.WriteLine($"Image saved to: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR:");
            Console.WriteLine(ex.Message);
        }
    }
}

