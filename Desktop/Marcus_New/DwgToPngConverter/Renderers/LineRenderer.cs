using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class LineRenderer : EntityRenderer<Line>
    {
        protected override void Draw(RenderContext context, Line line)
        {
            var start = context.ToScreenPoint(line.StartPoint);
            var end = context.ToScreenPoint(line.EndPoint);
            context.Canvas.DrawLine(start, end, context.Paint);
        }
    }
}
