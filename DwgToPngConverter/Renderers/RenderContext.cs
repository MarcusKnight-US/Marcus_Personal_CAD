using SkiaSharp;
using ACadSharp.Entities;
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
        public RenderResourceCache ResourceCache { get; }
        public string? DwgFilePath { get; }
        public Viewport? ActiveViewport { get; }
        public float TextMultiplier { get; }

        public float EffectiveScale => Scale * (ActiveViewport != null ? (float)ActiveViewport.ScaleFactor : 1f);

        public RenderContext(SKCanvas canvas, BoundingBox bbox, float scale, float offsetX, float offsetY, int height, SKPaint paint, RenderResourceCache resourceCache, string? dwgFilePath = null, Viewport? activeViewport = null, float textMultiplier = 1f)
        {
            Canvas = canvas;
            BoundingBox = bbox;
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Height = height;
            Paint = paint;
            ResourceCache = resourceCache;
            DwgFilePath = dwgFilePath;
            ActiveViewport = activeViewport;
            TextMultiplier = textMultiplier;
        }

        public SKPoint ToScreenPoint(double x, double y)
        {
            if (ActiveViewport != null)
            {
                var vp = ActiveViewport;
                double dx = x - vp.ViewTarget.X;
                double dy = y - vp.ViewTarget.Y;
                double px = vp.Center.X + (dx - vp.ViewCenter.X) * vp.ScaleFactor;
                double py = vp.Center.Y + (dy - vp.ViewCenter.Y) * vp.ScaleFactor;
                return new SKPoint(
                    TransformService.TransformX(px, BoundingBox.MinX, Scale, OffsetX),
                    TransformService.TransformY(py, BoundingBox.MinY, Scale, OffsetY, Height)
                );
            }
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
