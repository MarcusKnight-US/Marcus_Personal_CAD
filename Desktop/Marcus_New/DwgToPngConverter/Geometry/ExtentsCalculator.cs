namespace DwgToPngConverter.Geometry
{
    using ACadSharp.Entities;

    public static class ExtentsCalculator
    {
        // TryGetExtents computes min/max bounds for supported entity types.
        public static bool TryGetExtents(Entity entity, out Extents extents)
        {
            // LINE: determine min/max bounds from the two endpoints.
            if (entity is Line line)
            {
                double minX = Math.Min(line.StartPoint.X, line.EndPoint.X);
                double minY = Math.Min(line.StartPoint.Y, line.EndPoint.Y);

                double maxX = Math.Max(line.StartPoint.X, line.EndPoint.X);
                double maxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y);

                extents = new Extents(minX, minY, maxX, maxY);
                return true;
            }

            // CIRCLE: use center and radius to compute extents.
            if (entity is Circle circle)
            {
                double r = circle.Radius;

                extents = new Extents(
                    circle.Center.X - r,
                    circle.Center.Y - r,
                    circle.Center.X + r,
                    circle.Center.Y + r
                );
                return true;
            }

            // Unsupported entity type.
            extents = default;
            return false;
        }
    }
}
