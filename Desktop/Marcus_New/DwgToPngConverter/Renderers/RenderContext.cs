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
        public string? DwgFilePath { get; }

        public RenderContext(SKCanvas canvas, BoundingBox bbox, float scale, float offsetX, float offsetY, int height, SKPaint paint, string? dwgFilePath = null)
        {
            Canvas = canvas;
            BoundingBox = bbox;
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Height = height;
            Paint = paint;
            DwgFilePath = dwgFilePath;
        }

        public SKPoint ToScreenPoint(double x, double y)
        {
            return new SKPoint(
                TransformService.TransformX(x, BoundingBox.MinX, Scale, OffsetX),
                TransformService.TransformY(y, BoundingBox.MinY, Scale, OffsetY, Height)
            );
        }

        public SKPoint ToScreenPoint(CSMath.XY point)
        {
            return ToScreenPoint(point.X, point.Y);
        }

        public SKPoint ToScreenPoint(CSMath.XYZ point)
        {
            return ToScreenPoint(point.X, point.Y);
        }
    }
}
