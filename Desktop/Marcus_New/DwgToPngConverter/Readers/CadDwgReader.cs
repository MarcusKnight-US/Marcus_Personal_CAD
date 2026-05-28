using System.Collections.Generic;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;

namespace DwgToPngConverter.Readers
{
    public class CadDwgReader
    {
        // ReadAll loads the DWG file and returns every entity object found.
        public List<Entity> ReadAll(string path)
        {
            // Load DWG file from disk using ACadSharp.
            var doc = DwgReader.Read(path, null);
            return ReadAll(doc);
        }

        // ReadAll returns every entity object found in the already-loaded CadDocument.
        public List<Entity> ReadAll(CadDocument doc)
        {
            var entities = new List<Entity>();

            // Iterate through all model-space entities and collect them.
            foreach (var entity in doc.Entities)
            {
                if (entity != null)
                {
                    if (entity is Spline spline && (spline.ControlPoints == null || spline.ControlPoints.Count == 0) && spline.FitPoints != null && spline.FitPoints.Count > 0)
                    {
                        try
                        {
                            spline.UpdateFromFitPoints();
                        }
                        catch (System.Exception ex)
                        {
                            System.Console.WriteLine($"Warning: Failed to update spline from fit points: {ex.Message}");
                        }
                    }
                    entities.Add(entity);
                }
            }

            return entities;
        }
    }
}
