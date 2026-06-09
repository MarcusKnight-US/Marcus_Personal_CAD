namespace DwgToPngConverter.Geometry
{
    // Extents holds an axis-aligned rectangle used for bounding calculations.
    public struct Extents
    {
        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;

        public Extents(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }
    }
}