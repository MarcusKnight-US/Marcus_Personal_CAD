using System;
using System.Collections.Generic;
using ACadSharp.Entities;

namespace DwgToPngConverter.Scene
{
    public class CadScene
    {
        public static string? SheetNumber { get; set; } = null;

        public List<Entity> Entities { get; } = new List<Entity>();
        public DwgToPngConverter.Geometry.BoundingBox BoundingBox { get; } = new DwgToPngConverter.Geometry.BoundingBox();

        public void AddEntities(IEnumerable<Entity> entities)
        {
            if (entities == null)
            {
                return;
            }

            // Pre-pass: Find the layout sheet number "X" attribute from the "title" insert
            if (SheetNumber == null)
            {
                foreach (var entity in entities)
                {
                    if (entity is Insert insert && insert.Block != null && insert.Block.Name.Equals("title", StringComparison.OrdinalIgnoreCase))
                    {
                        if (insert.Attributes != null)
                        {
                            foreach (var attr in insert.Attributes)
                            {
                                if (attr != null && attr.Tag != null && attr.Tag.Equals("X", StringComparison.OrdinalIgnoreCase))
                                {
                                    SheetNumber = attr.Value;
                                    break;
                                }
                            }
                        }
                        if (SheetNumber != null) break;
                    }
                }
            }

            foreach (var entity in entities)
            {
                if (entity == null)
                {
                    continue;
                }

                if (entity.IsInvisible)
                {
                    continue;
                }

                if (entity.Layer != null && !(entity is Viewport))
                {
                    if (!entity.Layer.IsOn || (entity.Layer.Flags & ACadSharp.Tables.LayerFlags.Frozen) != ACadSharp.Tables.LayerFlags.None)
                    {
                        continue;
                    }
                }

                Entities.Add(entity);
                BoundingBox.AddEntity(entity);
            }
        }
    }
}
