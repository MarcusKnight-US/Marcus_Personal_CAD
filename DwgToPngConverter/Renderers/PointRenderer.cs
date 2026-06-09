using System;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class PointRenderer : EntityRenderer<Point>
    {
        protected override void Draw(RenderContext context, Point point)
        {
            if (point == null) return;

            var screenPos = context.ToScreenPoint(point.Location);

            // Screen-independent premium dot sizing for maximum clarity and clean visuals
            float radius = Math.Max(2f, context.Paint.StrokeWidth * 0.5f);

            using var paint = new SKPaint
            {
                Color = context.Paint.Color,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            context.Canvas.DrawCircle(screenPos.X, screenPos.Y, radius, paint);
        }
    }
}
