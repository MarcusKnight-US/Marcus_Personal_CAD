using System;
using System.Collections.Generic;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class EllipseRenderer : EntityRenderer<Ellipse>
    {
        private const double PrecisionUnitLength = 1.0;
        private const int MinPrecision = 16;
        private const int MaxPrecision = 256;

        protected override void Draw(RenderContext context, Ellipse ellipse)
        {
            // Calculate dynamic precision based on the approximate perimeter of the ellipse or arc.
            double majorLen = Math.Sqrt(
                ellipse.MajorAxisEndPoint.X * ellipse.MajorAxisEndPoint.X +
                ellipse.MajorAxisEndPoint.Y * ellipse.MajorAxisEndPoint.Y +
                ellipse.MajorAxisEndPoint.Z * ellipse.MajorAxisEndPoint.Z
            );
            double minorLen = majorLen * ellipse.RadiusRatio;
            double sweep = Math.Abs(ellipse.EndParameter - ellipse.StartParameter);
            if (sweep <= 0 || sweep > Math.Tau)
            {
                sweep = Math.Tau;
            }

            double approxCircumference = Math.PI * (majorLen + minorLen) * (sweep / Math.Tau);
            int precision = (int)Math.Ceiling(approxCircumference / PrecisionUnitLength);
            precision = Math.Clamp(precision, MinPrecision, MaxPrecision);

            List<CSMath.XYZ> points = ellipse.PolygonalVertexes(precision);
            if (points == null || points.Count < 2)
            {
                return;
            }

            using var path = new SKPath();
            path.MoveTo(context.ToScreenPoint(points[0]));

            for (int i = 1; i < points.Count; i++)
            {
                path.LineTo(context.ToScreenPoint(points[i]));
            }

            if (ellipse.IsFullEllipse)
            {
                path.Close();
            }

            context.Canvas.DrawPath(path, context.Paint);
        }
    }
}
