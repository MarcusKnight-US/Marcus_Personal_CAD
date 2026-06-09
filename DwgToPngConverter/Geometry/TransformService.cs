namespace DwgToPngConverter.Geometry
{
    public static class TransformService
    {
        public static float TransformX(double x, double minX, float scale, float offsetX)
        {
            return (float)((x - minX) * scale + offsetX);
        }

        public static float TransformY(double y, double minY, float scale, float offsetY, int height)
        {
            return height - (float)((y - minY) * scale + offsetY);
        }
    }
}
