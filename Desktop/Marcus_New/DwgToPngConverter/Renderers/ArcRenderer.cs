using System;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class ArcRenderer : EntityRenderer<Arc>
    {
        protected override void Draw(RenderContext context, Arc arc)
        {
            float cx = TransformService.TransformX(arc.Center.X, context.BoundingBox.MinX, context.Scale, context.OffsetX);
            float cy = TransformService.TransformY(arc.Center.Y, context.BoundingBox.MinY, context.Scale, context.OffsetY, context.Height);
            float radius = (float)(arc.Radius * context.Scale);

            var oval = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
            float startAngle = (float)(arc.StartAngle * 180.0 / Math.PI);
            float sweepAngle = (float)((arc.EndAngle - arc.StartAngle) * 180.0 / Math.PI);

            using var path = new SKPath();
            path.ArcTo(oval, startAngle, sweepAngle, false);
            context.Canvas.DrawPath(path, context.Paint);
        }
    }
}
