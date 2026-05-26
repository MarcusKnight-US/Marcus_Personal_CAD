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
            var center = context.ToScreenPoint(arc.Center);
            var radius = (float)(arc.Radius * context.Scale);
            var oval = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);
            float startAngle = (float)(arc.StartAngle * 180.0 / Math.PI);
            float sweepAngle = (float)((arc.EndAngle - arc.StartAngle) * 180.0 / Math.PI);

            using var path = new SKPath();
            path.ArcTo(oval, startAngle, sweepAngle, false);
            context.Canvas.DrawPath(path, context.Paint);
        }
    }
}
