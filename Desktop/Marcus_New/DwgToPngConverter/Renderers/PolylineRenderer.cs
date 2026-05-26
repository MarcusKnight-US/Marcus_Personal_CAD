using System;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class PolylineRenderer : EntityRenderer<LwPolyline>
    {
        protected override void Draw(RenderContext context, LwPolyline polyline)
        {
            var vertices = polyline.Vertices;
            if (vertices == null || vertices.Count < 2)
            {
                return;
            }

            using var path = new SKPath();
            path.MoveTo(context.ToScreenPoint(vertices[0].Location));

            for (int index = 1; index < vertices.Count; index++)
            {
                DrawSegment(path, vertices[index - 1], vertices[index], context);
            }

            if (polyline.IsClosed)
            {
                DrawSegment(path, vertices[^1], vertices[0], context);
                path.Close();
            }

            context.Canvas.DrawPath(path, context.Paint);
        }

        private static void DrawSegment(SKPath path, LwPolyline.Vertex startVertex, LwPolyline.Vertex endVertex, RenderContext context)
        {
            if (Math.Abs(startVertex.Bulge) < double.Epsilon)
            {
                path.LineTo(context.ToScreenPoint(endVertex.Location));
                return;
            }

            foreach (var point in GetArcPoints(startVertex.Location, endVertex.Location, startVertex.Bulge, context))
            {
                path.LineTo(point);
            }
        }

        private static IEnumerable<SKPoint> GetArcPoints(CSMath.XY start, CSMath.XY end, double bulge, RenderContext context)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var chordLength = Math.Sqrt(dx * dx + dy * dy);
            if (chordLength <= double.Epsilon)
            {
                yield return context.ToScreenPoint(end);
                yield break;
            }

            var theta = 4 * Math.Atan(bulge);
            var halfTheta = theta / 2.0;
            var radius = chordLength / (2 * Math.Abs(Math.Sin(halfTheta)));
            var midX = (start.X + end.X) / 2.0;
            var midY = (start.Y + end.Y) / 2.0;
            var normalX = -dy / chordLength;
            var normalY = dx / chordLength;
            if (bulge < 0)
            {
                normalX = -normalX;
                normalY = -normalY;
            }

            var centerX = midX + normalX * radius * Math.Cos(halfTheta);
            var centerY = midY + normalY * radius * Math.Cos(halfTheta);
            var startAngle = Math.Atan2(start.Y - centerY, start.X - centerX);
            var endAngle = Math.Atan2(end.Y - centerY, end.X - centerX);
            var sweep = endAngle - startAngle;
            if (bulge > 0 && sweep < 0)
            {
                sweep += Math.PI * 2;
            }
            else if (bulge < 0 && sweep > 0)
            {
                sweep -= Math.PI * 2;
            }

            int steps = Math.Max(6, (int)Math.Ceiling(Math.Abs(sweep) * 180.0 / Math.PI / 10.0));
            for (int i = 1; i < steps; i++)
            {
                var angle = startAngle + sweep * i / steps;
                var worldX = centerX + radius * Math.Cos(angle);
                var worldY = centerY + radius * Math.Sin(angle);
                yield return context.ToScreenPoint(new CSMath.XY(worldX, worldY));
            }

            yield return context.ToScreenPoint(end);
        }
    }
}
