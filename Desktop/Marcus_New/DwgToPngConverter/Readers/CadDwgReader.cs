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
            var entities = new List<Entity>();

            // Load DWG file from disk using ACadSharp.
            var doc = DwgReader.Read(path, null);

            // Iterate through all model-space entities and collect them.
            foreach (var entity in doc.Entities)
            {
                if (entity != null)
                {
                    entities.Add(entity);
                }
            }

            return entities;
        }
    }
}
