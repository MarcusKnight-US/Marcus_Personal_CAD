using System.Collections.Generic;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Scene
{
    public class CadScene
    {
        public List<Entity> Entities { get; } = new List<Entity>();
        public BoundingBox BoundingBox { get; } = new BoundingBox();

        public void AddEntities(IEnumerable<Entity> entities)
        {
            if (entities == null)
            {
                return;
            }

            foreach (var entity in entities)
            {
                if (entity == null)
                {
                    continue;
                }

                Entities.Add(entity);
                BoundingBox.AddEntity(entity);
            }
        }
    }
}
