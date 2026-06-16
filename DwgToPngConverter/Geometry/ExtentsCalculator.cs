namespace DwgToPngConverter.Geometry
{
    using System;
    using System.Collections.Generic;
    using CSMath;
    using ACadSharp.Entities;
    using ACadSharp.Objects;
    using ACadSharp.Tables;

    public static class ExtentsCalculator
    {
        private static readonly double[] CardinalAngles = new[] { 0.0, Math.PI / 2.0, Math.PI, Math.PI * 3.0 / 2.0 };

        [ThreadStatic]
        private static Dictionary<Entity, (bool Success, Extents Extents)>? _cache;

        private static Dictionary<Entity, (bool Success, Extents Extents)> Cache => _cache ??= new(ReferenceEqualityComparer.Instance);

        [ThreadStatic]
        private static Dictionary<BlockRecord, (bool Success, Extents Extents)>? _blockLocalCache;

        private static Dictionary<BlockRecord, (bool Success, Extents Extents)> BlockLocalCache => _blockLocalCache ??= new(ReferenceEqualityComparer.Instance);

        public static void ClearCache()
        {
            _cache?.Clear();
            _blockLocalCache?.Clear();
        }

        // TryGetExtents computes min/max bounds for supported entity types using Identity transformation.
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
            bool result = TryGetExtents(entity, Transformation.Identity, out extents);
            sw.Stop();
            PerformanceTracker.RecordExtents(entity.GetType().Name, sw.Elapsed.TotalMilliseconds);

            Cache[entity] = (result, extents);
            return result;
        }

        // TryGetExtents computes min/max bounds under a given Transformation matrix recursively.
        public static bool TryGetExtents(Entity entity, Transformation transform, out Extents extents)
        {
            if (entity == null || entity.IsInvisible)
            {
                extents = default;
                return false;
            }

            var activeTransform = (transform.M11 == 0 && transform.M12 == 0 && transform.M21 == 0 && transform.M22 == 0)
                ? Transformation.Identity
                : transform;

            if (entity is Line line)
            {
                var start = activeTransform.TransformPoint(line.StartPoint);
                var end = activeTransform.TransformPoint(line.EndPoint);
                extents = new Extents(
                    Math.Min(start.X, end.X),
                    Math.Min(start.Y, end.Y),
                    Math.Max(start.X, end.X),
                    Math.Max(start.Y, end.Y)
                );
                return true;
            }

            if (entity is Arc arc)
            {
                var center = activeTransform.TransformPoint(arc.Center);
                double scaleFactor = Math.Max(Math.Abs(activeTransform.ScaleX), Math.Abs(activeTransform.ScaleY));
                double radius = arc.Radius * scaleFactor;
                double startAngle = arc.StartAngle + activeTransform.Rotation;
                double endAngle = arc.EndAngle + activeTransform.Rotation;
                extents = GetArcExtents(new XY(center.X, center.Y), radius, startAngle, endAngle);
                return true;
            }

            if (entity is Circle circle)
            {
                var center = activeTransform.TransformPoint(circle.Center);
                double scaleFactor = Math.Max(Math.Abs(activeTransform.ScaleX), Math.Abs(activeTransform.ScaleY));
                double radius = circle.Radius * scaleFactor;
                extents = new Extents(
                    center.X - radius,
                    center.Y - radius,
                    center.X + radius,
                    center.Y + radius
                );
                return true;
            }

            if (entity is LwPolyline polyline)
            {
                extents = GetPolylineExtents(polyline, activeTransform);
                return true;
            }

            if (entity is Spline spline)
            {
                extents = GetSplineExtents(spline, activeTransform);
                return true;
            }

            if (entity is Ellipse ellipse)
            {
                extents = GetEllipseExtents(ellipse, activeTransform);
                return true;
            }

            if (entity is TextEntity text)
            {
                var insertPoint = activeTransform.TransformPoint(text.InsertPoint);
                double scaleFactor = Math.Max(Math.Abs(activeTransform.ScaleX), Math.Abs(activeTransform.ScaleY));
                double height = text.Height * scaleFactor;
                double rotation = text.Rotation + activeTransform.Rotation;
                extents = GetTextExtents(insertPoint, height, rotation, text.Value);
                return true;
            }

            if (entity is MText mtext)
            {
                var insertPoint = activeTransform.TransformPoint(mtext.InsertPoint);
                double scaleFactor = Math.Max(Math.Abs(activeTransform.ScaleX), Math.Abs(activeTransform.ScaleY));
                double height = mtext.Height * scaleFactor;
                double rotation = mtext.Rotation + activeTransform.Rotation;
                extents = GetTextExtents(insertPoint, height, rotation, mtext.Value);
                return true;
            }

            if (entity is Solid solid)
            {
                var p1 = activeTransform.TransformPoint(solid.FirstCorner);
                var p2 = activeTransform.TransformPoint(solid.SecondCorner);
                var p3 = activeTransform.TransformPoint(solid.ThirdCorner);
                var p4 = activeTransform.TransformPoint(solid.FourthCorner);
                double minX = Math.Min(Math.Min(p1.X, p2.X), Math.Min(p3.X, p4.X));
                double minY = Math.Min(Math.Min(p1.Y, p2.Y), Math.Min(p3.Y, p4.Y));
                double maxX = Math.Max(Math.Max(p1.X, p2.X), Math.Max(p3.X, p4.X));
                double maxY = Math.Max(Math.Max(p1.Y, p2.Y), Math.Max(p3.Y, p4.Y));
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

                var p0 = activeTransform.TransformPoint(rasterImage.InsertPoint);
                var uTrans = activeTransform.TransformVector(rasterImage.UVector);
                var vTrans = activeTransform.TransformVector(rasterImage.VVector);

                var p1 = p0 + w * uTrans;
                var p2 = p0 + h * vTrans;
                var p3 = p0 + w * uTrans + h * vTrans;

                double minX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
                double minY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
                double maxX = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
                double maxY = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));

                extents = new Extents(minX, minY, maxX, maxY);
                return true;
            }

            if (entity is Hatch hatch)
            {
                extents = GetHatchExtents(hatch, activeTransform);
                return true;
            }

            if (entity is Point point)
            {
                var loc = activeTransform.TransformPoint(point.Location);
                extents = new Extents(loc.X, loc.Y, loc.X, loc.Y);
                return true;
            }

            if (entity is Insert insert)
            {
                if (insert.Block == null)
                {
                    extents = default;
                    return false;
                }

                if (!TryGetBlockLocalExtents(insert.Block, out var localExt))
                {
                    extents = default;
                    return false;
                }

                int colCount = insert.ColumnCount == 0 ? 1 : insert.ColumnCount;
                int rowCount = insert.RowCount == 0 ? 1 : insert.RowCount;
                double colSpacing = insert.ColumnSpacing;
                double rowSpacing = insert.RowSpacing;

                bool success = false;
                var insertBBox = new BoundingBox();

                for (int col = 0; col < colCount; col++)
                {
                    for (int row = 0; row < rowCount; row++)
                    {
                        var localTransform = new Transformation(
                            insert.XScale, insert.YScale, insert.ZScale,
                            insert.Rotation,
                            insert.InsertPoint,
                            colSpacing, rowSpacing,
                            col, row
                        );

                        var combinedTransform = localTransform.Combine(activeTransform);

                        var c1 = combinedTransform.TransformPoint(new XY(localExt.MinX, localExt.MinY));
                        var c2 = combinedTransform.TransformPoint(new XY(localExt.MaxX, localExt.MinY));
                        var c3 = combinedTransform.TransformPoint(new XY(localExt.MaxX, localExt.MaxY));
                        var c4 = combinedTransform.TransformPoint(new XY(localExt.MinX, localExt.MaxY));

                        insertBBox.AddExtents(new Extents(
                            Math.Min(Math.Min(c1.X, c2.X), Math.Min(c3.X, c4.X)),
                            Math.Min(Math.Min(c1.Y, c2.Y), Math.Min(c3.Y, c4.Y)),
                            Math.Max(Math.Max(c1.X, c2.X), Math.Max(c3.X, c4.X)),
                            Math.Max(Math.Max(c1.Y, c2.Y), Math.Max(c3.Y, c4.Y))
                        ));
                        success = true;
                    }
                }

                extents = success ? new Extents(insertBBox.MinX, insertBBox.MinY, insertBBox.MaxX, insertBBox.MaxY) : default;
                return success;
            }

            if (entity is Dimension dimension)
            {
                if (dimension.Block == null)
                {
                    extents = default;
                    return false;
                }

                if (!TryGetBlockLocalExtents(dimension.Block, out var localExt))
                {
                    extents = default;
                    return false;
                }

                var c1 = activeTransform.TransformPoint(new XY(localExt.MinX, localExt.MinY));
                var c2 = activeTransform.TransformPoint(new XY(localExt.MaxX, localExt.MinY));
                var c3 = activeTransform.TransformPoint(new XY(localExt.MaxX, localExt.MaxY));
                var c4 = activeTransform.TransformPoint(new XY(localExt.MinX, localExt.MaxY));

                extents = new Extents(
                    Math.Min(Math.Min(c1.X, c2.X), Math.Min(c3.X, c4.X)),
                    Math.Min(Math.Min(c1.Y, c2.Y), Math.Min(c3.Y, c4.Y)),
                    Math.Max(Math.Max(c1.X, c2.X), Math.Max(c3.X, c4.X)),
                    Math.Max(Math.Max(c1.Y, c2.Y), Math.Max(c3.Y, c4.Y))
                );
                return true;
            }

            if (entity is MultiLeader mleader)
            {
                if (mleader.ContextData == null)
                {
                    extents = default;
                    return false;
                }

                var bbox = new BoundingBox();
                bool success = false;

                // 1. Leader lines
                if (mleader.ContextData.LeaderRoots != null)
                {
                    foreach (var root in mleader.ContextData.LeaderRoots)
                    {
                        if (root == null) continue;
                        var conn = activeTransform.TransformPoint(root.ConnectionPoint);
                        bbox.AddExtents(new Extents(conn.X, conn.Y, conn.X, conn.Y));
                        success = true;

                        if (root.Lines != null)
                        {
                            foreach (var leaderLine in root.Lines)
                            {
                                if (leaderLine == null || leaderLine.Points == null) continue;
                                foreach (var pt in leaderLine.Points)
                                {
                                    var tpt = activeTransform.TransformPoint(pt);
                                    bbox.AddExtents(new Extents(tpt.X, tpt.Y, tpt.X, tpt.Y));
                                }
                            }
                        }
                    }
                }

                // 2. Content
                if (mleader.ContentType == LeaderContentType.MText && !string.IsNullOrEmpty(mleader.ContextData.TextLabel))
                {
                    var textLoc = activeTransform.TransformPoint(mleader.ContextData.TextLocation);
                    var textExt = GetTextExtents(textLoc, mleader.ContextData.TextHeight > 0 ? mleader.ContextData.TextHeight : 1.0, mleader.ContextData.TextRotation + activeTransform.Rotation, mleader.ContextData.TextLabel);
                    bbox.AddExtents(textExt);
                    success = true;
                }
                else if (mleader.ContentType == LeaderContentType.Block && mleader.ContextData.BlockContent != null)
                {
                    if (TryGetBlockLocalExtents(mleader.ContextData.BlockContent, out var localExt))
                    {
                        var blockLoc = mleader.ContextData.BlockContentLocation;
                        var blockScale = mleader.ContextData.BlockContentScale;
                        var blockRot = mleader.ContextData.BlockContentRotation;

                        var blockTransform = new Transformation(
                            blockScale.X, blockScale.Y, blockScale.Z,
                            blockRot,
                            blockLoc
                        );

                        var combinedTransform = blockTransform.Combine(activeTransform);

                        var c1 = combinedTransform.TransformPoint(new XY(localExt.MinX, localExt.MinY));
                        var c2 = combinedTransform.TransformPoint(new XY(localExt.MaxX, localExt.MinY));
                        var c3 = combinedTransform.TransformPoint(new XY(localExt.MaxX, localExt.MaxY));
                        var c4 = combinedTransform.TransformPoint(new XY(localExt.MinX, localExt.MaxY));

                        bbox.AddExtents(new Extents(
                            Math.Min(Math.Min(c1.X, c2.X), Math.Min(c3.X, c4.X)),
                            Math.Min(Math.Min(c1.Y, c2.Y), Math.Min(c3.Y, c4.Y)),
                            Math.Max(Math.Max(c1.X, c2.X), Math.Max(c3.X, c4.X)),
                            Math.Max(Math.Max(c1.Y, c2.Y), Math.Max(c3.Y, c4.Y))
                        ));
                        success = true;
                    }
                }

                extents = success ? new Extents(bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY) : default;
                return success;
            }

            extents = default;
            return false;
        }

        private static bool TryGetBlockLocalExtents(BlockRecord block, out Extents extents)
        {
            if (block == null)
            {
                extents = default;
                return false;
            }

            if (BlockLocalCache.TryGetValue(block, out var cached))
            {
                extents = cached.Extents;
                return cached.Success;
            }

            bool success = false;
            var blockBBox = new BoundingBox();
            foreach (var entity in block.Entities)
            {
                if (entity == null || entity.IsInvisible) continue;
                if (TryGetExtents(entity, Transformation.Identity, out var childExt))
                {
                    blockBBox.AddExtents(childExt);
                    success = true;
                }
            }

            extents = success ? new Extents(blockBBox.MinX, blockBBox.MinY, blockBBox.MaxX, blockBBox.MaxY) : default;
            BlockLocalCache[block] = (success, extents);
            return success;
        }

        private static Extents GetHatchExtents(Hatch hatch, Transformation transform)
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
                            var start = transform.TransformPoint(lineEdge.Start);
                            var end = transform.TransformPoint(lineEdge.End);
                            var segmentExtents = new Extents(
                                Math.Min(start.X, end.X),
                                Math.Min(start.Y, end.Y),
                                Math.Max(start.X, end.X),
                                Math.Max(start.Y, end.Y)
                            );
                            extents = hasExtents ? AddExtents(extents, segmentExtents) : segmentExtents;
                            hasExtents = true;
                        }
                        else if (edge is Hatch.BoundaryPath.Arc arcEdge)
                        {
                            var center = transform.TransformPoint(arcEdge.Center);
                            double scaleFactor = Math.Max(Math.Abs(transform.ScaleX), Math.Abs(transform.ScaleY));
                            double radius = arcEdge.Radius * scaleFactor;
                            double startAngle = arcEdge.StartAngle + transform.Rotation;
                            double endAngle = arcEdge.EndAngle + transform.Rotation;
                            var segmentExtents = GetArcExtents(center, radius, startAngle, endAngle);
                            extents = hasExtents ? AddExtents(extents, segmentExtents) : segmentExtents;
                            hasExtents = true;
                        }
                        else if (edge is Hatch.BoundaryPath.Polyline polyEdge && polyEdge.Vertices != null)
                        {
                            foreach (var v in polyEdge.Vertices)
                            {
                                var pt = transform.TransformPoint(new XY(v.X, v.Y));
                                var ptExtents = new Extents(pt.X, pt.Y, pt.X, pt.Y);
                                extents = hasExtents ? AddExtents(extents, ptExtents) : ptExtents;
                                hasExtents = true;
                            }
                        }
                        else if (edge is Hatch.BoundaryPath.Ellipse ellEdge)
                        {
                            var center = transform.TransformPoint(ellEdge.Center);
                            var majorVector = transform.TransformVector(ellEdge.MajorAxisEndPoint);
                            
                            double cx = center.X;
                            double cy = center.Y;
                            double mx = majorVector.X;
                            double my = majorVector.Y;
                            double r = ellEdge.RadiusRatio;

                            // Closed-form O(1) mathematically exact tight bounding box for the transformed ellipse
                            double dx = Math.Sqrt(mx * mx + (my * r) * (my * r));
                            double dy = Math.Sqrt(my * my + (mx * r) * (mx * r));

                            var segmentExtents = new Extents(cx - dx, cy - dy, cx + dx, cy + dy);
                            extents = hasExtents ? AddExtents(extents, segmentExtents) : segmentExtents;
                            hasExtents = true;
                        }
                        else if (edge is Hatch.BoundaryPath.Spline splineEdge && splineEdge.ControlPoints != null)
                        {
                            foreach (var cp in splineEdge.ControlPoints)
                            {
                                var pt = transform.TransformPoint(cp);
                                var ptExtents = new Extents(pt.X, pt.Y, pt.X, pt.Y);
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
                        if (subEntity != null && TryGetExtents(subEntity, transform, out var subExtents))
                        {
                            extents = hasExtents ? AddExtents(extents, subExtents) : subExtents;
                            hasExtents = true;
                        }
                    }
                }
            }

            return extents;
        }

        private static Extents GetEllipseExtents(Ellipse ellipse, Transformation transform)
        {
            var center = transform.TransformPoint(ellipse.Center);
            var majorVector = transform.TransformVector(ellipse.MajorAxisEndPoint);

            double cx = center.X;
            double cy = center.Y;
            double mx = majorVector.X;
            double my = majorVector.Y;
            double r = ellipse.RadiusRatio;

            // Closed-form O(1) mathematically exact tight bounding box for the ellipse
            double dx = Math.Sqrt(mx * mx + (my * r) * (my * r));
            double dy = Math.Sqrt(my * my + (mx * r) * (mx * r));

            return new Extents(cx - dx, cy - dy, cx + dx, cy + dy);
        }

        private static Extents GetArcExtents(XY center, double radius, double startAngle, double endAngle)
        {
            var startX = center.X + radius * Math.Cos(startAngle);
            var startY = center.Y + radius * Math.Sin(startAngle);
            var endX = center.X + radius * Math.Cos(endAngle);
            var endY = center.Y + radius * Math.Sin(endAngle);

            var extents = new Extents(
                Math.Min(startX, endX),
                Math.Min(startY, endY),
                Math.Max(startX, endX),
                Math.Max(startY, endY)
            );

            var sweep = endAngle - startAngle;
            if (sweep < 0)
            {
                sweep += Math.Tau;
            }

            foreach (var angle in CardinalAngles)
            {
                if (ArcContainsAngle(startAngle, sweep, angle))
                {
                    var px = center.X + radius * Math.Cos(angle);
                    var py = center.Y + radius * Math.Sin(angle);
                    extents = AddExtents(extents, new Extents(px, py, px, py));
                }
            }

            return extents;
        }

        private static Extents GetArcExtents(Arc arc)
        {
            return GetArcExtents(new XY(arc.Center.X, arc.Center.Y), arc.Radius, arc.StartAngle, arc.EndAngle);
        }

        private static Extents GetSplineExtents(Spline spline, Transformation transform)
        {
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
                    var tcp = transform.TransformPoint(cp);
                    if (tcp.X < minX) minX = tcp.X;
                    if (tcp.Y < minY) minY = tcp.Y;
                    if (tcp.X > maxX) maxX = tcp.X;
                    if (tcp.Y > maxY) maxY = tcp.Y;
                    hasPoints = true;
                }
            }

            if (spline.FitPoints != null && spline.FitPoints.Count > 0)
            {
                foreach (var fp in spline.FitPoints)
                {
                    var tfp = transform.TransformPoint(fp);
                    if (tfp.X < minX) minX = tfp.X;
                    if (tfp.Y < minY) minY = tfp.Y;
                    if (tfp.X > maxX) maxX = tfp.X;
                    if (tfp.Y > maxY) maxY = tfp.Y;
                    hasPoints = true;
                }
            }

            if (!hasPoints)
            {
                return default;
            }

            return new Extents(minX, minY, maxX, maxY);
        }

        private static Extents GetSplineExtents(Spline spline)
        {
            return GetSplineExtents(spline, Transformation.Identity);
        }

        private static Extents GetPolylineExtents(LwPolyline polyline, Transformation transform)
        {
            var vertices = polyline.Vertices;
            if (vertices == null || vertices.Count == 0)
            {
                return default;
            }

            var p0 = transform.TransformPoint(vertices[0].Location);
            var extents = new Extents(p0.X, p0.Y, p0.X, p0.Y);
            for (int index = 1; index < vertices.Count; index++)
            {
                extents = AddSegmentExtents(extents, vertices[index - 1], vertices[index], transform);
            }

            if (polyline.IsClosed)
            {
                extents = AddSegmentExtents(extents, vertices[^1], vertices[0], transform);
            }

            return extents;
        }

        private static Extents GetPolylineExtents(LwPolyline polyline)
        {
            return GetPolylineExtents(polyline, Transformation.Identity);
        }

        private static Extents AddSegmentExtents(Extents current, LwPolyline.Vertex startVertex, LwPolyline.Vertex endVertex, Transformation transform)
        {
            var start = transform.TransformPoint(startVertex.Location);
            var end = transform.TransformPoint(endVertex.Location);

            if (Math.Abs(startVertex.Bulge) < double.Epsilon)
            {
                return AddExtents(current, new Extents(
                    Math.Min(start.X, end.X),
                    Math.Min(start.Y, end.Y),
                    Math.Max(start.X, end.X),
                    Math.Max(start.Y, end.Y)
                ));
            }

            return AddExtents(current, GetArcExtentsFromBulge(start, end, startVertex.Bulge));
        }

        private static Extents AddSegmentExtents(Extents current, LwPolyline.Vertex startVertex, LwPolyline.Vertex endVertex)
        {
            return AddSegmentExtents(current, startVertex, endVertex, Transformation.Identity);
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
