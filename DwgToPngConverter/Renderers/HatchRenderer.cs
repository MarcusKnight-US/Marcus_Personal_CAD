using System;
using System.Collections.Generic;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class HatchRenderer : EntityRenderer<Hatch>
    {
        protected override void Draw(RenderContext context, Hatch hatch)
        {
            if (hatch == null || hatch.Paths == null || hatch.Paths.Count == 0) return;

            using var path = new SKPath();

            foreach (var boundaryPath in hatch.Paths)
            {
                if (boundaryPath == null) continue;

                // 1. Process Edges
                if (boundaryPath.Edges != null && boundaryPath.Edges.Count > 0)
                {
                    bool isFirst = true;
                    foreach (var edge in boundaryPath.Edges)
                    {
                        if (edge == null) continue;

                        if (edge is Hatch.BoundaryPath.Line lineEdge)
                        {
                            var pStart = context.ToScreenPoint(lineEdge.Start.X, lineEdge.Start.Y);
                            var pEnd = context.ToScreenPoint(lineEdge.End.X, lineEdge.End.Y);
                            if (isFirst)
                            {
                                path.MoveTo(pStart);
                                isFirst = false;
                            }
                            path.LineTo(pEnd);
                        }
                        else if (edge is Hatch.BoundaryPath.Arc arcEdge)
                        {
                            var center = context.ToScreenPoint(arcEdge.Center.X, arcEdge.Center.Y);
                            var radius = (float)(arcEdge.Radius * context.EffectiveScale);
                            var oval = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

                            double sweep = arcEdge.EndAngle - arcEdge.StartAngle;
                            if (sweep < 0) sweep += Math.Tau;

                            float startAngle = (float)(-(arcEdge.StartAngle + context.CurrentTransformation.Rotation) * 180.0 / Math.PI);
                            float sweepAngle = (float)(-sweep * 180.0 / Math.PI);

                            if (!arcEdge.CounterClockWise)
                            {
                                sweep = arcEdge.StartAngle - arcEdge.EndAngle;
                                if (sweep < 0) sweep += Math.Tau;
                                startAngle = (float)(-(arcEdge.EndAngle + context.CurrentTransformation.Rotation) * 180.0 / Math.PI);
                                sweepAngle = (float)(sweep * 180.0 / Math.PI);
                            }

                            if (isFirst)
                            {
                                // Estimate start point of arc to MoveTo
                                double startRad = arcEdge.CounterClockWise ? arcEdge.StartAngle : arcEdge.EndAngle;
                                var pStart = context.ToScreenPoint(
                                    arcEdge.Center.X + arcEdge.Radius * Math.Cos(startRad),
                                    arcEdge.Center.Y + arcEdge.Radius * Math.Sin(startRad)
                                );
                                path.MoveTo(pStart);
                                isFirst = false;
                            }

                            path.ArcTo(oval, startAngle, sweepAngle, false);
                        }
                        else if (edge is Hatch.BoundaryPath.Ellipse ellEdge)
                        {
                            double majX = ellEdge.MajorAxisEndPoint.X;
                            double majY = ellEdge.MajorAxisEndPoint.Y;
                            double majLen = Math.Sqrt(majX * majX + majY * majY);
                            double minLen = majLen * ellEdge.RadiusRatio;
                            double angle = Math.Atan2(majY, majX);

                            int steps = 36;
                            double sweep = ellEdge.EndAngle - ellEdge.StartAngle;
                            if (sweep < 0) sweep += Math.Tau;

                            for (int step = 0; step <= steps; step++)
                            {
                                double t = ellEdge.StartAngle + sweep * (step / (double)steps);
                                double lx = majLen * Math.Cos(t);
                                double ly = minLen * Math.Sin(t);
                                double ax = ellEdge.Center.X + lx * Math.Cos(angle) - ly * Math.Sin(angle);
                                double ay = ellEdge.Center.Y + lx * Math.Sin(angle) + ly * Math.Cos(angle);
                                var p = context.ToScreenPoint(ax, ay);

                                if (isFirst && step == 0)
                                {
                                    path.MoveTo(p);
                                    isFirst = false;
                                }
                                else
                                {
                                    path.LineTo(p);
                                }
                            }
                        }
                        else if (edge is Hatch.BoundaryPath.Polyline polyEdge)
                        {
                            if (polyEdge.Vertices == null || polyEdge.Vertices.Count == 0) continue;

                            for (int i = 0; i < polyEdge.Vertices.Count; i++)
                            {
                                var v = polyEdge.Vertices[i];
                                var p = context.ToScreenPoint(v.X, v.Y);
                                if (isFirst && i == 0)
                                {
                                    path.MoveTo(p);
                                    isFirst = false;
                                }
                                else
                                {
                                    path.LineTo(p);
                                }
                            }
                            if (polyEdge.IsClosed)
                            {
                                path.Close();
                            }
                        }
                        else if (edge is Hatch.BoundaryPath.Spline splineEdge)
                        {
                            if (splineEdge.FitPoints != null && splineEdge.FitPoints.Count > 0)
                            {
                                for (int i = 0; i < splineEdge.FitPoints.Count; i++)
                                {
                                    var fp = splineEdge.FitPoints[i];
                                    var p = context.ToScreenPoint(fp.X, fp.Y);
                                    if (isFirst && i == 0)
                                    {
                                        path.MoveTo(p);
                                        isFirst = false;
                                    }
                                    else
                                    {
                                        path.LineTo(p);
                                    }
                                }
                            }
                            else if (splineEdge.ControlPoints != null && splineEdge.ControlPoints.Count > 0)
                            {
                                for (int i = 0; i < splineEdge.ControlPoints.Count; i++)
                                {
                                    var cp = splineEdge.ControlPoints[i];
                                    var p = context.ToScreenPoint(cp.X, cp.Y);
                                    if (isFirst && i == 0)
                                    {
                                        path.MoveTo(p);
                                        isFirst = false;
                                    }
                                    else
                                    {
                                        path.LineTo(p);
                                    }
                                }
                            }
                        }
                    }
                }

                // 2. Process Entities
                if (boundaryPath.Entities != null && boundaryPath.Entities.Count > 0)
                {
                    foreach (var entity in boundaryPath.Entities)
                    {
                        if (entity == null) continue;

                        if (entity is Line line)
                        {
                            var p1 = context.ToScreenPoint(line.StartPoint.X, line.StartPoint.Y);
                            var p2 = context.ToScreenPoint(line.EndPoint.X, line.EndPoint.Y);
                            path.MoveTo(p1);
                            path.LineTo(p2);
                        }
                        else if (entity is Circle circle)
                        {
                            var c = context.ToScreenPoint(circle.Center.X, circle.Center.Y);
                            var r = (float)(circle.Radius * context.EffectiveScale);
                            path.AddCircle(c.X, c.Y, r);
                        }
                        else if (entity is Arc arc)
                        {
                            var center = context.ToScreenPoint(arc.Center);
                            var radius = (float)(arc.Radius * context.EffectiveScale);
                            var oval = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);
                            double sweep = arc.EndAngle - arc.StartAngle;
                            if (sweep < 0) sweep += Math.Tau;
                            float startAngle = (float)(-(arc.StartAngle + context.CurrentTransformation.Rotation) * 180.0 / Math.PI);
                            float sweepAngle = (float)(-sweep * 180.0 / Math.PI);
                            
                            var pStart = context.ToScreenPoint(
                                arc.Center.X + arc.Radius * Math.Cos(arc.StartAngle),
                                arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngle)
                            );
                            path.MoveTo(pStart);
                            path.ArcTo(oval, startAngle, sweepAngle, false);
                        }
                        else if (entity is LwPolyline polyline)
                        {
                            if (polyline.Vertices == null || polyline.Vertices.Count == 0) continue;
                            for (int i = 0; i < polyline.Vertices.Count; i++)
                            {
                                var v = polyline.Vertices[i];
                                var p = context.ToScreenPoint(v.Location.X, v.Location.Y);
                                if (i == 0) path.MoveTo(p);
                                else path.LineTo(p);
                            }
                            if (polyline.IsClosed) path.Close();
                        }
                    }
                }
            }

            // Solid/Transparent Fill Setup for premium design aesthetics
            var fillColor = context.Paint.Color;
            if (!hatch.IsSolid)
            {
                fillColor = new SKColor(fillColor.Red, fillColor.Green, fillColor.Blue, 90); // ~35% opacity
            }
            var fillPaint = context.ResourceCache.GetPaint(fillColor, SKPaintStyle.Fill, isAntialias: true);

            context.Canvas.DrawPath(path, fillPaint);

            // Outlines are drawn cleanly with a 0.5f thin stroke to define pattern edges
            float strokeWidth = Math.Max(0.5f, context.Paint.StrokeWidth * 0.2f);
            var strokePaint = context.ResourceCache.GetPaint(context.Paint.Color, SKPaintStyle.Stroke, isAntialias: true, strokeWidth: strokeWidth);
            context.Canvas.DrawPath(path, strokePaint);
        }
    }
}
