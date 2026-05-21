using SkiaSharp;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public readonly struct RenderContext
    {
        public SKCanvas Canvas { get; }
        public BoundingBox BoundingBox { get; }
        public float Scale { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public int Height { get; }
        public SKPaint Paint { get; }

        public RenderContext(SKCanvas canvas, BoundingBox bbox, float scale, float offsetX, float offsetY, int height, SKPaint paint)
        {
            Canvas = canvas;
            BoundingBox = bbox;
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Height = height;
            Paint = paint;
        }
    }
}
