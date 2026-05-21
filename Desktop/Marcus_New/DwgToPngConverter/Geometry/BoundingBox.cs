namespace DwgToPngConverter.Geometry
{
    using System.Collections.Generic;
    using ACadSharp.Entities;

    public class BoundingBox
    {
        // Initialize the box to an empty state.
        public double MinX = double.MaxValue;
        public double MinY = double.MaxValue;
        public double MaxX = double.MinValue;
        public double MaxY = double.MinValue;

        // Add a single CAD entity to the bounding box.
        public void AddEntity(Entity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Try to compute the entity extents; skip unsupported entities.
            if (!ExtentsCalculator.TryGetExtents(entity, out var extents))
            {
                return;
            }

            AddExtents(extents);
        }

        // Add a sequence of entities to the bounding box.
        public void AddEntities(IEnumerable<Entity> entities)
        {
            if (entities == null)
            {
                return;
            }

            foreach (var entity in entities)
            {
                AddEntity(entity);
            }
        }

        // Expand the box using a computed extents rectangle.
        public void AddExtents(Extents ext)
        {
            if (ext.MinX < MinX) MinX = ext.MinX;
            if (ext.MinY < MinY) MinY = ext.MinY;
            if (ext.MaxX > MaxX) MaxX = ext.MaxX;
            if (ext.MaxY > MaxY) MaxY = ext.MaxY;
        }

        // True when the bounding box has not been updated.
        public bool IsEmpty => MinX == double.MaxValue || MinY == double.MaxValue || MaxX == double.MinValue || MaxY == double.MinValue;

        // Size properties are safe when the box is empty.
        public double Width => IsEmpty ? 0 : MaxX - MinX;
        public double Height => IsEmpty ? 0 : MaxY - MinY;
    }
}
