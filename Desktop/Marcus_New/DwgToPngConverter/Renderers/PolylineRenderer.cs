using System.Linq;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class PolylineRenderer : EntityRenderer<LwPolyline>
    {
        protected override void Draw(RenderContext context, LwPolyline polyline)
        {
            if (polyline.Vertices == null || polyline.Vertices.Count == 0)
            {
                return;
            }

            using var path = new SKPath();
            var first = polyline.Vertices[0];
            path.MoveTo(TransformService.TransformX(first.Location.X, context.BoundingBox.MinX, context.Scale, context.OffsetX), TransformService.TransformY(first.Location.Y, context.BoundingBox.MinY, context.Scale, context.OffsetY, context.Height));

            foreach (var vertex in polyline.Vertices.Skip(1))
            {
                path.LineTo(TransformService.TransformX(vertex.Location.X, context.BoundingBox.MinX, context.Scale, context.OffsetX), TransformService.TransformY(vertex.Location.Y, context.BoundingBox.MinY, context.Scale, context.OffsetY, context.Height));
            }

            context.Canvas.DrawPath(path, context.Paint);
        }
    }
}
