using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class CircleRenderer : EntityRenderer<Circle>
    {
        protected override void Draw(RenderContext context, Circle circle)
        {
            float cx = TransformService.TransformX(circle.Center.X, context.BoundingBox.MinX, context.Scale, context.OffsetX);
            float cy = TransformService.TransformY(circle.Center.Y, context.BoundingBox.MinY, context.Scale, context.OffsetY, context.Height);
            float radius = (float)(circle.Radius * context.Scale);

            context.Canvas.DrawCircle(cx, cy, radius, context.Paint);
        }
    }
}
