using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class LineRenderer : EntityRenderer<Line>
    {
        protected override void Draw(RenderContext context, Line line)
        {
            float x1 = TransformService.TransformX(line.StartPoint.X, context.BoundingBox.MinX, context.Scale, context.OffsetX);
            float y1 = TransformService.TransformY(line.StartPoint.Y, context.BoundingBox.MinY, context.Scale, context.OffsetY, context.Height);

            float x2 = TransformService.TransformX(line.EndPoint.X, context.BoundingBox.MinX, context.Scale, context.OffsetX);
            float y2 = TransformService.TransformY(line.EndPoint.Y, context.BoundingBox.MinY, context.Scale, context.OffsetY, context.Height);

            context.Canvas.DrawLine(x1, y1, x2, y2, context.Paint);
        }
    }
}
