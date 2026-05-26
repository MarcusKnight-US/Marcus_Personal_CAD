namespace DwgToPngConverter.Geometry
{
    using System;
    using ACadSharp.Entities;

    public static class ExtentsCalculator
    {
        // TryGetExtents computes min/max bounds for supported entity types.
        public static bool TryGetExtents(Entity entity, out Extents extents)
        {
            if (entity is Line line)
            {
                extents = new Extents(
                    Math.Min(line.StartPoint.X, line.EndPoint.X),
                    Math.Min(line.StartPoint.Y, line.EndPoint.Y),
                    Math.Max(line.StartPoint.X, line.EndPoint.X),
                    Math.Max(line.StartPoint.Y, line.EndPoint.Y)
                );
                return true;
            }

            if (entity is Circle circle)
            {
                double radius = circle.Radius;
                extents = new Extents(
                    circle.Center.X - radius,
                    circle.Center.Y - radius,
                    circle.Center.X + radius,
                    circle.Center.Y + radius
                );
                return true;
            }

            if (entity is LwPolyline polyline)
            {
                extents = GetPolylineExtents(polyline);
                return true;
            }

            if (entity is Spline spline)
            {
                extents = GetSplineExtents(spline);
                return true;
            }

            extents = default;
            return false;
        }

        private static Extents GetSplineExtents(Spline spline)
        {
            var points = GetSplineSamplePoints(spline, 64);
            if (points == null || points.Count == 0)
            {
                return default;
            }

            var extents = new Extents(points[0].X, points[0].Y, points[0].X, points[0].Y);
            for (int i = 1; i < points.Count; i++)
            {
                extents = AddExtents(extents, new Extents(points[i].X, points[i].Y, points[i].X, points[i].Y));
            }

            return extents;
        }

        private static List<CSMath.XYZ> GetSplineSamplePoints(Spline spline, int precision)
        {
            precision = Math.Max(2, precision);
            if (spline.TryPolygonalVertexes(precision, out var points) && points.Count > 1)
            {
                return points;
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

            return points;
        }

        private static Extents GetPolylineExtents(LwPolyline polyline)
        {
            var vertices = polyline.Vertices;
            if (vertices == null || vertices.Count == 0)
            {
                return default;
            }

            var extents = new Extents(vertices[0].Location.X, vertices[0].Location.Y, vertices[0].Location.X, vertices[0].Location.Y);
            for (int index = 1; index < vertices.Count; index++)
            {
                extents = AddSegmentExtents(extents, vertices[index - 1], vertices[index]);
            }

            if (polyline.IsClosed)
            {
                extents = AddSegmentExtents(extents, vertices[^1], vertices[0]);
            }

            return extents;
        }

        private static Extents AddSegmentExtents(Extents current, LwPolyline.Vertex startVertex, LwPolyline.Vertex endVertex)
        {
            if (Math.Abs(startVertex.Bulge) < double.Epsilon)
            {
                return AddExtents(current, new Extents(
                    Math.Min(startVertex.Location.X, endVertex.Location.X),
                    Math.Min(startVertex.Location.Y, endVertex.Location.Y),
                    Math.Max(startVertex.Location.X, endVertex.Location.X),
                    Math.Max(startVertex.Location.Y, endVertex.Location.Y)
                ));
            }

            return AddExtents(current, GetArcExtentsFromBulge(startVertex.Location, endVertex.Location, startVertex.Bulge));
        }

        private static Extents AddExtents(Extents current, Extents other)
        {
            return new Extents(
                Math.Min(current.MinX, other.MinX),
                Math.Min(current.MinY, other.MinY),
                Math.Max(current.MaxX, other.MaxX),
                Math.Max(current.MaxY, other.MaxY)
            );
        }

        private static Extents GetArcExtentsFromBulge(CSMath.XY start, CSMath.XY end, double bulge)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var chordLength = Math.Sqrt(dx * dx + dy * dy);
            if (chordLength <= double.Epsilon)
            {
                return new Extents(start.X, start.Y, start.X, start.Y);
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

            var extents = new Extents(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Max(start.X, end.X),
                Math.Max(start.Y, end.Y)
            );

            var cardinalAngles = new[] { 0.0, Math.PI / 2.0, Math.PI, Math.PI * 3.0 / 2.0 };
            foreach (var angle in cardinalAngles)
            {
                if (ArcContainsAngle(startAngle, sweep, angle))
                {
                    var px = centerX + radius * Math.Cos(angle);
                    var py = centerY + radius * Math.Sin(angle);
                    extents = AddExtents(extents, new Extents(px, py, px, py));
                }
            }

            return extents;
        }

        private static bool ArcContainsAngle(double startAngle, double sweep, double angle)
        {
            var normalizedStart = NormalizeAngle(startAngle);
            var target = NormalizeAngle(angle);
            var normalizedSweep = NormalizeAngle(sweep);

            if (Math.Abs(normalizedSweep) < double.Epsilon)
            {
                return false;
            }

            if (normalizedSweep > 0)
            {
                return AngleIsBetween(normalizedStart, normalizedStart + normalizedSweep, target);
            }

            return AngleIsBetween(normalizedStart + normalizedSweep, normalizedStart, target);
        }

        private static double NormalizeAngle(double angle)
        {
            var result = angle % (Math.PI * 2);
            if (result < 0)
            {
                result += Math.PI * 2;
            }

            return result;
        }

        private static bool AngleIsBetween(double start, double end, double target)
        {
            if (end >= start)
            {
                return target >= start && target <= end;
            }

            return target >= start || target <= end;
        }
    }
}
