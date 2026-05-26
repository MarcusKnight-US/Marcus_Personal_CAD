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
            // In CAD, arcs always sweep CCW from StartAngle to EndAngle.
            double sweep = arc.EndAngle - arc.StartAngle;
            if (sweep < 0)
            {
                sweep += Math.PI * 2;
            }

            // Since our screen coordinates invert the Y-axis (Y goes down),
            // the angle direction is reversed. We must negate both the start angle
            // and the sweep angle to render the arc in the correct position and orientation.
            float startAngle = (float)(-arc.StartAngle * 180.0 / Math.PI);
            float sweepAngle = (float)(-sweep * 180.0 / Math.PI);

            using var path = new SKPath();
            path.ArcTo(oval, startAngle, sweepAngle, false);
            context.Canvas.DrawPath(path, context.Paint);
        }
    }
}
