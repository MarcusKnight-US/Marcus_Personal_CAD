using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class CircleRenderer : EntityRenderer<Circle>
    {
        protected override void Draw(RenderContext context, Circle circle)
        {
            var center = context.ToScreenPoint(circle.Center);
            var radius = (float)(circle.Radius * context.Scale);
            context.Canvas.DrawCircle(center, radius, context.Paint);
        }
    }
}
