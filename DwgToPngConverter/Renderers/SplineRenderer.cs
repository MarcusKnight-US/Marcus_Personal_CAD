using System;
using System.Collections.Generic;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class SplineRenderer : EntityRenderer<Spline>
    {
        // Configurable parameter: length of curve represented by one segment.
        // E.g., if PrecisionUnitLength = 2.0, a spline of length 100 will have 50 segments.
        // Decreasing this value increases rendering quality for all splines.
        private const double PrecisionUnitLength = 1.0;

        private const int MinPrecision = 8;
        private const int MaxPrecision = 512;

        protected override void Draw(RenderContext context, Spline spline)
        {
            double length = ApproximateLength(spline);
            int precision = (int)Math.Ceiling(length * context.TransformationScale / PrecisionUnitLength);
            if (precision < MinPrecision) precision = MinPrecision;
            if (precision > MaxPrecision) precision = MaxPrecision;

            if (!TryGetPolylinePoints(spline, precision, out var points) || points.Count < 2)
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

        private static double ApproximateLength(Spline spline)
        {
            const int CoarseSteps = 10;
            if (spline.TryPolygonalVertexes(CoarseSteps, out var points) && points.Count > 1)
            {
                double len = 0;
                for (int i = 1; i < points.Count; i++)
                {
                    len += Distance(points[i - 1], points[i]);
                }
                return len;
            }

            double coarseLen = 0;
            bool hasPrev = false;
            CSMath.XYZ prev = default;
            for (int i = 0; i <= CoarseSteps; i++)
            {
                var t = (double)i / CoarseSteps;
                if (spline.TryPointOnSpline(t, out var curr))
                {
                    if (hasPrev)
                    {
                        coarseLen += Distance(prev, curr);
                    }
                    prev = curr;
                    hasPrev = true;
                }
            }
            return coarseLen;
        }

        private static double Distance(CSMath.XYZ p1, CSMath.XYZ p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double dz = p2.Z - p1.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
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
