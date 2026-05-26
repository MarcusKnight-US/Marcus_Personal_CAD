using System.Collections.Generic;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class SplineRenderer : EntityRenderer<Spline>
    {
        private const int DefaultPrecision = 64;

        protected override void Draw(RenderContext context, Spline spline)
        {
            if (!TryGetPolylinePoints(spline, DefaultPrecision, out var points) || points.Count < 2)
            {
                return;
            }

            using var path = new SKPath();
            path.MoveTo(context.ToScreenPoint(points[0]));

            for (int i = 1; i < points.Count; i++)
            {
                path.LineTo(context.ToScreenPoint(points[i]));
            }

            if (spline.IsClosed)
            {
                path.Close();
            }

            context.Canvas.DrawPath(path, context.Paint);
        }

        private static bool TryGetPolylinePoints(Spline spline, int precision, out List<CSMath.XYZ> points)
        {
            precision = Math.Max(2, precision);
            points = new List<CSMath.XYZ>();

            if (spline.TryPolygonalVertexes(precision, out points) && points.Count > 1)
            {
                return true;
            }

            points = new List<CSMath.XYZ>(precision + 1);
            for (int i = 0; i <= precision; i++)
            {
                var t = (double)i / precision;
                if (spline.TryPointOnSpline(t, out var position))
                {
                    points.Add(position);
                }
            }

            return points.Count > 1;
        }
    }
}
