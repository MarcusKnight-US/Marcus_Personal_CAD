namespace DwgToPngConverter.Geometry
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using CSMath;
    using ACadSharp.Entities;

    public static class ExtentsCalculator
    {
        private static readonly double[] CardinalAngles = new[] { 0.0, Math.PI / 2.0, Math.PI, Math.PI * 3.0 / 2.0 };

        [ThreadStatic]
        private static Dictionary<Entity, (bool Success, Extents Extents)>? _cache;

        private static Dictionary<Entity, (bool Success, Extents Extents)> Cache => _cache ??= new(ReferenceEqualityComparer.Instance);

        public static void ClearCache()
        {
            _cache?.Clear();
        }

        // TryGetExtents computes min/max bounds for supported entity types.
        public static bool TryGetExtents(Entity entity, out Extents extents)
        {
            if (entity == null)
            {
                extents = default;
                return false;
            }

            if (Cache.TryGetValue(entity, out var cached))
            {
                extents = cached.Extents;
                return cached.Success;
            }

            if (entity.IsInvisible)
            {
                extents = default;
                Cache[entity] = (false, extents);
                return false;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool result = TryGetExtentsInternal(entity, out extents);
            sw.Stop();
            PerformanceTracker.RecordExtents(entity.GetType().Name, sw.Elapsed.TotalMilliseconds);

            Cache[entity] = (result, extents);
            return result;
        }

        private static bool TryGetExtentsInternal(Entity entity, out Extents extents)
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

            if (entity is Arc arc)
            {
                extents = GetArcExtents(arc);
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

            if (entity is Ellipse ellipse)
            {
                extents = GetEllipseExtents(ellipse);
                return true;
            }

            if (entity is TextEntity text)
            {
                extents = GetTextExtents(text.InsertPoint, text.Height, text.Rotation, text.Value);
                return true;
            }

            if (entity is MText mtext)
            {
                extents = GetTextExtents(mtext.InsertPoint, mtext.Height, mtext.Rotation, mtext.Value);
                return true;
            }

            if (entity is Solid solid)
            {
                double minX = Math.Min(Math.Min(solid.FirstCorner.X, solid.SecondCorner.X), Math.Min(solid.ThirdCorner.X, solid.FourthCorner.X));
                double minY = Math.Min(Math.Min(solid.FirstCorner.Y, solid.SecondCorner.Y), Math.Min(solid.ThirdCorner.Y, solid.FourthCorner.Y));
                double maxX = Math.Max(Math.Max(solid.FirstCorner.X, solid.SecondCorner.X), Math.Max(solid.ThirdCorner.X, solid.FourthCorner.X));
                double maxY = Math.Max(Math.Max(solid.FirstCorner.Y, solid.SecondCorner.Y), Math.Max(solid.ThirdCorner.Y, solid.FourthCorner.Y));
                extents = new Extents(minX, minY, maxX, maxY);
                return true;
            }

            if (entity is RasterImage rasterImage)
            {
                double w = 1.0;
                double h = 1.0;
                if (rasterImage.Definition != null)
                {
                    w = rasterImage.Definition.Size.X;
                    h = rasterImage.Definition.Size.Y;
                }

                var p0 = rasterImage.InsertPoint;
                var p1 = p0 + w * rasterImage.UVector;
                var p2 = p0 + h * rasterImage.VVector;
                var p3 = p0 + w * rasterImage.UVector + h * rasterImage.VVector;

                double minX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
                double minY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
                double maxX = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
                double maxY = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));

                extents = new Extents(minX, minY, maxX, maxY);
                return true;
            }

            if (entity is Hatch hatch)
            {
                extents = GetHatchExtents(hatch);
                return true;
            }

            if (entity is Point point)
            {
                extents = new Extents(point.Location.X, point.Location.Y, point.Location.X, point.Location.Y);
                return true;
            }

            extents = default;
            return false;
        }

        private static Extents GetHatchExtents(Hatch hatch)
        {
            if (hatch.Paths == null || hatch.Paths.Count == 0)
            {
                return default;
            }

            bool hasExtents = false;
            Extents extents = default;

            foreach (var path in hatch.Paths)
            {
                if (path == null) continue;

                // 1. Process Edges
                if (path.Edges != null && path.Edges.Count > 0)
                {
                    foreach (var edge in path.Edges)
                    {
                        if (edge == null) continue;

                        if (edge is Hatch.BoundaryPath.Line lineEdge)
                        {
                            var segmentExtents = new Extents(
                                Math.Min(lineEdge.Start.X, lineEdge.End.X),
                                Math.Min(lineEdge.Start.Y, lineEdge.End.Y),
                                Math.Max(lineEdge.Start.X, lineEdge.End.X),
                                Math.Max(lineEdge.Start.Y, lineEdge.End.Y)
                            );
                            extents = hasExtents ? AddExtents(extents, segmentExtents) : segmentExtents;
                            hasExtents = true;
                        }
                        else if (edge is Hatch.BoundaryPath.Arc arcEdge)
                        {
                            var segmentExtents = new Extents(
                                arcEdge.Center.X - arcEdge.Radius,
                                arcEdge.Center.Y - arcEdge.Radius,
                                arcEdge.Center.X + arcEdge.Radius,
                                arcEdge.Center.Y + arcEdge.Radius
                            );
                            extents = hasExtents ? AddExtents(extents, segmentExtents) : segmentExtents;
                            hasExtents = true;
                        }
                        else if (edge is Hatch.BoundaryPath.Polyline polyEdge && polyEdge.Vertices != null)
                        {
                            foreach (var v in polyEdge.Vertices)
                            {
                                var ptExtents = new Extents(v.X, v.Y, v.X, v.Y);
                                extents = hasExtents ? AddExtents(extents, ptExtents) : ptExtents;
                                hasExtents = true;
                            }
                        }
                        else if (edge is Hatch.BoundaryPath.Ellipse ellEdge)
                        {
                            var segmentExtents = new Extents(
                                ellEdge.Center.X - ellEdge.MajorAxis,
                                ellEdge.Center.Y - ellEdge.MajorAxis,
                                ellEdge.Center.X + ellEdge.MajorAxis,
                                ellEdge.Center.Y + ellEdge.MajorAxis
                            );
                            extents = hasExtents ? AddExtents(extents, segmentExtents) : segmentExtents;
                            hasExtents = true;
                        }
                        else if (edge is Hatch.BoundaryPath.Spline splineEdge && splineEdge.ControlPoints != null)
                        {
                            foreach (var cp in splineEdge.ControlPoints)
                            {
                                var ptExtents = new Extents(cp.X, cp.Y, cp.X, cp.Y);
                                extents = hasExtents ? AddExtents(extents, ptExtents) : ptExtents;
                                hasExtents = true;
                            }
                        }
                    }
                }

                // 2. Process Entities
                if (path.Entities != null && path.Entities.Count > 0)
                {
                    foreach (var subEntity in path.Entities)
                    {
                        if (subEntity != null && TryGetExtents(subEntity, out var subExtents))
                        {
                            extents = hasExtents ? AddExtents(extents, subExtents) : subExtents;
                            hasExtents = true;
                        }
                    }
                }
            }

            return extents;
        }

        private static Extents GetEllipseExtents(Ellipse ellipse)
        {
            double cx = ellipse.Center.X;
            double cy = ellipse.Center.Y;
            double mx = ellipse.MajorAxisEndPoint.X;
            double my = ellipse.MajorAxisEndPoint.Y;
            double r = ellipse.RadiusRatio;

            // Closed-form O(1) mathematically exact tight bounding box for the ellipse
            double dx = Math.Sqrt(mx * mx + (my * r) * (my * r));
            double dy = Math.Sqrt(my * my + (mx * r) * (mx * r));

            return new Extents(cx - dx, cy - dy, cx + dx, cy + dy);
        }

        private static Extents GetArcExtents(Arc arc)
        {
            var startX = arc.Center.X + arc.Radius * Math.Cos(arc.StartAngle);
            var startY = arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngle);
            var endX = arc.Center.X + arc.Radius * Math.Cos(arc.EndAngle);
            var endY = arc.Center.Y + arc.Radius * Math.Sin(arc.EndAngle);

            var extents = new Extents(
                Math.Min(startX, endX),
                Math.Min(startY, endY),
                Math.Max(startX, endX),
                Math.Max(startY, endY)
            );

            var sweep = arc.EndAngle - arc.StartAngle;
            if (sweep < 0)
            {
                sweep += Math.Tau;
            }

            foreach (var angle in CardinalAngles)
            {
                if (ArcContainsAngle(arc.StartAngle, sweep, angle))
                {
                    var px = arc.Center.X + arc.Radius * Math.Cos(angle);
                    var py = arc.Center.Y + arc.Radius * Math.Sin(angle);
                    extents = AddExtents(extents, new Extents(px, py, px, py));
                }
            }

            return extents;
        }

        private static Extents GetSplineExtents(Spline spline)
        {
            // O(N) convex hull of control points/fit points. In B-splines, the curve is mathematically
            // guaranteed to lie inside the convex hull of its control points, making this extremely fast and robust.
            if ((spline.ControlPoints == null || spline.ControlPoints.Count == 0) &&
                (spline.FitPoints == null || spline.FitPoints.Count == 0))
            {
                return default;
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            bool hasPoints = false;

            if (spline.ControlPoints != null && spline.ControlPoints.Count > 0)
            {
                foreach (var cp in spline.ControlPoints)
                {
                    if (cp.X < minX) minX = cp.X;
                    if (cp.Y < minY) minY = cp.Y;
                    if (cp.X > maxX) maxX = cp.X;
                    if (cp.Y > maxY) maxY = cp.Y;
                    hasPoints = true;
                }
            }

            if (spline.FitPoints != null && spline.FitPoints.Count > 0)
            {
                foreach (var fp in spline.FitPoints)
                {
                    if (fp.X < minX) minX = fp.X;
                    if (fp.Y < minY) minY = fp.Y;
                    if (fp.X > maxX) maxX = fp.X;
                    if (fp.Y > maxY) maxY = fp.Y;
                    hasPoints = true;
                }
            }

            if (!hasPoints)
            {
                return default;
            }

            return new Extents(minX, minY, maxX, maxY);
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
                sweep += Math.Tau;
            }
            else if (bulge < 0 && sweep > 0)
            {
                sweep -= Math.Tau;
            }

            var extents = new Extents(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Max(start.X, end.X),
                Math.Max(start.Y, end.Y)
            );

            foreach (var angle in CardinalAngles)
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
            var result = angle % Math.Tau;
            if (result < 0)
            {
                result += Math.Tau;
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

        private static Extents GetTextExtents(XYZ insertPoint, double height, double rotation, string textValue)
        {
            if (string.IsNullOrEmpty(textValue))
            {
                return new Extents(insertPoint.X, insertPoint.Y, insertPoint.X, insertPoint.Y);
            }

            double cleanLength = CleanMText(textValue).Length;
            double approxWidth = cleanLength * 0.6 * height;
            double minLocalX = 0;
            double maxLocalX = approxWidth;
            double minLocalY = -0.2 * height;
            double maxLocalY = 0.8 * height;

            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);

            double rx1 = minLocalX * cos - minLocalY * sin + insertPoint.X;
            double ry1 = minLocalX * sin + minLocalY * cos + insertPoint.Y;

            double rx2 = maxLocalX * cos - minLocalY * sin + insertPoint.X;
            double ry2 = maxLocalX * sin + minLocalY * cos + insertPoint.Y;

            double rx3 = maxLocalX * cos - maxLocalY * sin + insertPoint.X;
            double ry3 = maxLocalX * sin + maxLocalY * cos + insertPoint.Y;

            double rx4 = minLocalX * cos - maxLocalY * sin + insertPoint.X;
            double ry4 = minLocalX * sin + maxLocalY * cos + insertPoint.Y;

            double minX = Math.Min(Math.Min(rx1, rx2), Math.Min(rx3, rx4));
            double minY = Math.Min(Math.Min(ry1, ry2), Math.Min(ry3, ry4));
            double maxX = Math.Max(Math.Max(rx1, rx2), Math.Max(rx3, rx4));
            double maxY = Math.Max(Math.Max(ry1, ry2), Math.Max(ry3, ry4));

            return new Extents(minX, minY, maxX, maxY);
        }

        private static string CleanMText(string text)
        {
            return MTextHelper.CleanMText(text);
        }
    }
}
