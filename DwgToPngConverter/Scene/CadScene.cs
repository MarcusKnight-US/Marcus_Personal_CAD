using System;
using System.Collections.Generic;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Objects;
using DwgToPngConverter.Geometry;
using CSMath;

namespace DwgToPngConverter.Scene
{
    public class CadScene
    {
        public static string? SheetNumber { get; set; } = null;

        public List<Entity> Entities { get; } = new List<Entity>();
        public DwgToPngConverter.Geometry.BoundingBox BoundingBox { get; } = new DwgToPngConverter.Geometry.BoundingBox();

        public void AddEntities(IEnumerable<Entity> entities)
        {
            if (entities == null)
            {
                return;
            }

            // Pre-pass: Find the layout sheet number "X" attribute from the "title" insert
            if (SheetNumber == null)
            {
                foreach (var entity in entities)
                {
                    if (entity is Insert insert && insert.Block != null && insert.Block.Name.Equals("title", StringComparison.OrdinalIgnoreCase))
                    {
                        if (insert.Attributes != null)
                        {
                            foreach (var attr in insert.Attributes)
                            {
                                if (attr != null && attr.Tag != null && attr.Tag.Equals("X", StringComparison.OrdinalIgnoreCase))
                                {
                                    SheetNumber = attr.Value;
                                    break;
                                }
                            }
                        }
                        if (SheetNumber != null) break;
                    }
                }
            }

            foreach (var entity in entities)
            {
                if (entity == null)
                {
                    continue;
                }

                if (entity.Layer != null && !(entity is Viewport))
                {
                    if (!entity.Layer.IsOn || (entity.Layer.Flags & ACadSharp.Tables.LayerFlags.Frozen) != ACadSharp.Tables.LayerFlags.None)
                    {
                        continue;
                    }
                }

                if (entity is Insert insert)
                {
                    var exploded = ExplodeInsert(insert);
                    AddEntities(exploded);
                }
                else if (entity is Dimension dimension)
                {
                    var identity = new Transformation(1.0, 1.0, 1.0, 0.0, new XYZ(0, 0, 0));
                    var exploded = ExplodeDimension(dimension, identity);
                    AddEntities(exploded);
                }
                else if (entity is MultiLeader mleader)
                {
                    var identity = new Transformation(1.0, 1.0, 1.0, 0.0, new XYZ(0, 0, 0));
                    var exploded = ExplodeMultiLeader(mleader, identity);
                    AddEntities(exploded);
                }
                else
                {
                    Entities.Add(entity);
                    BoundingBox.AddEntity(entity);
                }
            }
        }

        private static List<Entity> ExplodeInsert(Insert insert, Dictionary<string, string>? parentAttribDict = null)
        {
            var result = new List<Entity>();
            if (insert.Block == null)
            {
                return result;
            }

            var attribDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (insert.Attributes != null)
            {
                foreach (var attr in insert.Attributes)
                {
                    if (attr != null && attr.Tag != null)
                    {
                        attribDict[attr.Tag] = attr.Value ?? "";
                    }
                }
            }

            if (parentAttribDict != null)
            {
                foreach (var kvp in parentAttribDict)
                {
                    attribDict[kvp.Key] = kvp.Value;
                }
            }


            int colCount = insert.ColumnCount == 0 ? 1 : insert.ColumnCount;
            int rowCount = insert.RowCount == 0 ? 1 : insert.RowCount;
            double colSpacing = insert.ColumnSpacing;
            double rowSpacing = insert.RowSpacing;

            foreach (var entity in insert.Block.Entities)
            {
                if (entity == null) continue;

                for (int col = 0; col < colCount; col++)
                {
                    for (int row = 0; row < rowCount; row++)
                    {
                        var transform = new Transformation(
                            insert.XScale, insert.YScale, insert.ZScale,
                            insert.Rotation,
                            insert.InsertPoint,
                            colSpacing, rowSpacing,
                            col, row
                        );

                        var exploded = ExplodeAndTransform(
                            entity,
                            transform,
                            insert.Color,
                            insert.Layer,
                            insert.LineWeight,
                            attribDict
                        );
                        result.AddRange(exploded);
                    }
                }
            }

            return result;
        }

        private static List<Entity> ExplodeAndTransform(
            Entity entity,
            Transformation transform,
            ACadSharp.Color? parentColor = null,
            ACadSharp.Tables.Layer? parentLayer = null,
            LineWeightType? parentLineWeight = null,
            Dictionary<string, string>? attribDict = null)
        {
            var list = new List<Entity>();

            // Resolve ByBlock color
            ACadSharp.Color? entityColor = entity.Color;
            if (entityColor == null)
            {
                entityColor = ACadSharp.Color.ByLayer;
            }
            if (entityColor.Value.IsByBlock && parentColor != null)
            {
                entityColor = parentColor;
            }

            // Resolve Layer "0" inheritance
            ACadSharp.Tables.Layer entityLayer = entity.Layer;
            if (entityLayer != null && entityLayer.Name == "0" && parentLayer != null)
            {
                entityLayer = parentLayer;
            }

            // Resolve ByBlock lineweight
            LineWeightType entityLineWeight = entity.LineWeight;
            if (entityLineWeight == LineWeightType.ByBlock && parentLineWeight != null)
            {
                entityLineWeight = parentLineWeight.Value;
            }

            if (entity is Insert childInsert)
            {
                var childExploded = ExplodeInsert(childInsert, attribDict);
                foreach (var childEntity in childExploded)
                {
                    var transformed = ExplodeAndTransform(
                        childEntity,
                        transform,
                        entityColor,
                        entityLayer,
                        entityLineWeight,
                        attribDict
                    );
                    list.AddRange(transformed);
                }
            }
            else if (entity is Dimension dimension)
            {
                var exploded = ExplodeDimension(dimension, transform, entityColor, entityLayer, entityLineWeight);
                list.AddRange(exploded);
            }
            else if (entity is MultiLeader mleader)
            {
                var exploded = ExplodeMultiLeader(mleader, transform, entityColor, entityLayer, entityLineWeight);
                list.AddRange(exploded);
            }
            else if (entity is Line line)
            {
                list.Add(new Line()
                {
                    StartPoint = transform.TransformPoint(line.StartPoint),
                    EndPoint = transform.TransformPoint(line.EndPoint),
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = line.LineType,
                    LineWeight = entityLineWeight
                });
            }
            else if (entity is Circle circle)
            {
                list.Add(new Circle()
                {
                    Center = transform.TransformPoint(circle.Center),
                    Radius = circle.Radius * Math.Max(Math.Abs(transform.ScaleX), Math.Abs(transform.ScaleY)),
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = circle.LineType,
                    LineWeight = entityLineWeight
                });
            }
            else if (entity is Arc arc)
            {
                list.Add(new Arc()
                {
                    Center = transform.TransformPoint(arc.Center),
                    Radius = arc.Radius * Math.Max(Math.Abs(transform.ScaleX), Math.Abs(transform.ScaleY)),
                    StartAngle = arc.StartAngle + transform.Rotation,
                    EndAngle = arc.EndAngle + transform.Rotation,
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = arc.LineType,
                    LineWeight = entityLineWeight
                });
            }
            else if (entity is Ellipse ellipse)
            {
                list.Add(new Ellipse()
                {
                    Center = transform.TransformPoint(ellipse.Center),
                    MajorAxisEndPoint = transform.TransformVector(ellipse.MajorAxisEndPoint),
                    RadiusRatio = ellipse.RadiusRatio,
                    StartParameter = ellipse.StartParameter,
                    EndParameter = ellipse.EndParameter,
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = ellipse.LineType,
                    LineWeight = entityLineWeight
                });
            }
            else if (entity is LwPolyline polyline)
            {
                var newPolyline = new LwPolyline()
                {
                    IsClosed = polyline.IsClosed,
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = polyline.LineType,
                    LineWeight = entityLineWeight
                };
                foreach (var vertex in polyline.Vertices)
                {
                    var transformedLocation = transform.TransformPoint(vertex.Location);
                    var newVertex = new LwPolyline.Vertex(transformedLocation)
                    {
                        Bulge = vertex.Bulge,
                        StartWidth = vertex.StartWidth,
                        EndWidth = vertex.EndWidth
                    };
                    newPolyline.Vertices.Add(newVertex);
                }
                list.Add(newPolyline);
            }
            else if (entity is Spline spline)
            {
                var newSpline = new Spline()
                {
                    Degree = spline.Degree,
                    IsClosed = spline.IsClosed,
                    IsPeriodic = spline.IsPeriodic,
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = spline.LineType,
                    LineWeight = entityLineWeight
                };
                newSpline.Knots.AddRange(spline.Knots);
                newSpline.Weights.AddRange(spline.Weights);
                foreach (var cp in spline.ControlPoints)
                {
                    newSpline.ControlPoints.Add(transform.TransformPoint(cp));
                }
                foreach (var fp in spline.FitPoints)
                {
                    newSpline.FitPoints.Add(transform.TransformPoint(fp));
                }
                if (newSpline.ControlPoints.Count == 0 && newSpline.FitPoints.Count > 0)
                {
                    try
                    {
                        newSpline.UpdateFromFitPoints();
                    }
                    catch {}
                }
                list.Add(newSpline);
            }
            else if (entity is AttributeDefinition attrDef)
            {
                string val = attrDef.Value ?? "";
                string tag = attrDef.Tag ?? "";

                bool found = false;
                if (attribDict != null)
                {
                    if (attribDict.TryGetValue(tag, out var v))
                    {
                        val = v;
                        found = true;
                    }
                    else if (tag.Equals("VIEWNUMBER", StringComparison.OrdinalIgnoreCase) && attribDict.TryGetValue("#", out v))
                    {
                        val = v;
                        found = true;
                    }
                    else if (tag.Equals("VIEWTITLE", StringComparison.OrdinalIgnoreCase) && attribDict.TryGetValue("VIEWNAME", out v))
                    {
                        val = v;
                        found = true;
                    }
                }

                if (!found && tag.Equals("SHEETNUMBER", StringComparison.OrdinalIgnoreCase) && SheetNumber != null)
                {
                    val = SheetNumber;
                }

                if (tag.Equals("X", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val))
                {
                    SheetNumber = val;
                }

                list.Add(new TextEntity()
                {
                    Value = val,
                    InsertPoint = transform.TransformPoint(attrDef.InsertPoint),
                    Height = attrDef.Height * Math.Max(Math.Abs(transform.ScaleX), Math.Abs(transform.ScaleY)),
                    Rotation = attrDef.Rotation + transform.Rotation,
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = attrDef.LineType,
                    LineWeight = entityLineWeight
                });
            }
            else if (entity is TextEntity text)
            {
                list.Add(new TextEntity()
                {
                    Value = text.Value,
                    InsertPoint = transform.TransformPoint(text.InsertPoint),
                    Height = text.Height * Math.Max(Math.Abs(transform.ScaleX), Math.Abs(transform.ScaleY)),
                    Rotation = text.Rotation + transform.Rotation,
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = text.LineType,
                    LineWeight = entityLineWeight
                });
            }
            else if (entity is MText mtext)
            {
                var newMText = new MText()
                {
                    Value = mtext.Value,
                    InsertPoint = transform.TransformPoint(mtext.InsertPoint),
                    Height = mtext.Height * Math.Max(Math.Abs(transform.ScaleX), Math.Abs(transform.ScaleY)),
                    AttachmentPoint = mtext.AttachmentPoint,
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = mtext.LineType,
                    LineWeight = entityLineWeight
                };
                double newAngle = mtext.Rotation + transform.Rotation;
                newMText.AlignmentPoint = new XYZ(Math.Cos(newAngle), Math.Sin(newAngle), 0.0);
                list.Add(newMText);
            }
            else if (entity is Solid solid)
            {
                list.Add(new Solid()
                {
                    FirstCorner = transform.TransformPoint(solid.FirstCorner),
                    SecondCorner = transform.TransformPoint(solid.SecondCorner),
                    ThirdCorner = transform.TransformPoint(solid.ThirdCorner),
                    FourthCorner = transform.TransformPoint(solid.FourthCorner),
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = solid.LineType,
                    LineWeight = entityLineWeight
                });
            }
            else if (entity is RasterImage rasterImage)
            {
                list.Add(new RasterImage(rasterImage.Definition)
                {
                    InsertPoint = transform.TransformPoint(rasterImage.InsertPoint),
                    UVector = transform.TransformVector(rasterImage.UVector),
                    VVector = transform.TransformVector(rasterImage.VVector),
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = rasterImage.LineType,
                    LineWeight = entityLineWeight
                });
            }
            else if (entity is Hatch hatch)
            {
                var newHatch = new Hatch()
                {
                    IsSolid = hatch.IsSolid,
                    Pattern = hatch.Pattern,
                    PatternAngle = hatch.PatternAngle,
                    PatternScale = hatch.PatternScale,
                    PatternType = hatch.PatternType,
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = hatch.LineType,
                    LineWeight = entityLineWeight
                };

                if (hatch.Paths != null)
                {
                    foreach (var path in hatch.Paths)
                    {
                        if (path == null) continue;

                        var newPath = new Hatch.BoundaryPath()
                        {
                            Flags = path.Flags
                        };

                        if (path.Edges != null && path.Edges.Count > 0)
                        {
                            foreach (var edge in path.Edges)
                            {
                                if (edge == null) continue;

                                if (edge is Hatch.BoundaryPath.Line lineEdge)
                                {
                                    newPath.Edges.Add(new Hatch.BoundaryPath.Line()
                                    {
                                        Start = transform.TransformPoint(lineEdge.Start),
                                        End = transform.TransformPoint(lineEdge.End)
                                    });
                                }
                                else if (edge is Hatch.BoundaryPath.Arc arcEdge)
                                {
                                    newPath.Edges.Add(new Hatch.BoundaryPath.Arc()
                                    {
                                        Center = transform.TransformPoint(arcEdge.Center),
                                        Radius = arcEdge.Radius * Math.Max(Math.Abs(transform.ScaleX), Math.Abs(transform.ScaleY)),
                                        StartAngle = arcEdge.StartAngle + transform.Rotation,
                                        EndAngle = arcEdge.EndAngle + transform.Rotation,
                                        CounterClockWise = arcEdge.CounterClockWise
                                    });
                                }
                                else if (edge is Hatch.BoundaryPath.Ellipse ellEdge)
                                {
                                    newPath.Edges.Add(new Hatch.BoundaryPath.Ellipse()
                                    {
                                        Center = transform.TransformPoint(ellEdge.Center),
                                        MajorAxisEndPoint = transform.TransformPoint(ellEdge.MajorAxisEndPoint),
                                        RadiusRatio = ellEdge.RadiusRatio,
                                        StartAngle = ellEdge.StartAngle + transform.Rotation,
                                        EndAngle = ellEdge.EndAngle + transform.Rotation,
                                        CounterClockWise = ellEdge.CounterClockWise
                                    });
                                }
                                else if (edge is Hatch.BoundaryPath.Polyline polyEdge)
                                {
                                    var newPoly = new Hatch.BoundaryPath.Polyline()
                                    {
                                        IsClosed = polyEdge.IsClosed
                                    };
                                    if (polyEdge.Vertices != null)
                                    {
                                        foreach (var v in polyEdge.Vertices)
                                        {
                                            var pt2D = transform.TransformPoint(new XY(v.X, v.Y));
                                            newPoly.Vertices.Add(new XYZ(pt2D.X, pt2D.Y, v.Z));
                                        }
                                    }
                                    newPath.Edges.Add(newPoly);
                                }
                                else if (edge is Hatch.BoundaryPath.Spline splineEdge)
                                {
                                    var newSpline = new Hatch.BoundaryPath.Spline()
                                    {
                                        Degree = splineEdge.Degree,
                                        IsPeriodic = splineEdge.IsPeriodic,
                                        IsRational = splineEdge.IsRational,
                                        StartTangent = transform.TransformPoint(splineEdge.StartTangent),
                                        EndTangent = transform.TransformPoint(splineEdge.EndTangent)
                                    };
                                    if (splineEdge.ControlPoints != null)
                                    {
                                        foreach (var cp in splineEdge.ControlPoints)
                                        {
                                            newSpline.ControlPoints.Add(transform.TransformPoint(cp));
                                        }
                                    }
                                    if (splineEdge.FitPoints != null)
                                    {
                                        foreach (var fp in splineEdge.FitPoints)
                                        {
                                            newSpline.FitPoints.Add(transform.TransformPoint(fp));
                                        }
                                    }
                                    if (splineEdge.Knots != null)
                                    {
                                        newSpline.Knots.AddRange(splineEdge.Knots);
                                    }
                                    newPath.Edges.Add(newSpline);
                                }
                            }
                        }

                        if (path.Entities != null && path.Entities.Count > 0)
                        {
                            foreach (var pathEnt in path.Entities)
                            {
                                if (pathEnt == null) continue;
                                var transformedEnts = ExplodeAndTransform(pathEnt, transform, entityColor, entityLayer, entityLineWeight);
                                newPath.Entities.AddRange(transformedEnts);
                            }
                        }

                        newHatch.Paths.Add(newPath);
                    }
                }

                list.Add(newHatch);
            }
            else if (entity is Point point)
            {
                list.Add(new Point()
                {
                    Location = transform.TransformPoint(point.Location),
                    Color = entityColor.Value,
                    Layer = entityLayer,
                    LineType = point.LineType,
                    LineWeight = entityLineWeight
                });
            }

            return list;
        }

        private static List<Entity> ExplodeDimension(
            Dimension dimension,
            Transformation transform,
            ACadSharp.Color? parentColor = null,
            ACadSharp.Tables.Layer? parentLayer = null,
            LineWeightType? parentLineWeight = null)
        {
            var list = new List<Entity>();
            if (dimension == null || dimension.Block == null)
            {
                return list;
            }

            ACadSharp.Color? dimensionColor = dimension.Color;
            if (dimensionColor == null) dimensionColor = ACadSharp.Color.ByLayer;
            if (dimensionColor.Value.IsByBlock && parentColor != null) dimensionColor = parentColor;

            ACadSharp.Tables.Layer dimensionLayer = dimension.Layer;
            if (dimensionLayer != null && dimensionLayer.Name == "0" && parentLayer != null) dimensionLayer = parentLayer;

            LineWeightType dimensionLineWeight = dimension.LineWeight;
            if (dimensionLineWeight == LineWeightType.ByBlock && parentLineWeight != null)
            {
                dimensionLineWeight = parentLineWeight.Value;
            }

            foreach (var entity in dimension.Block.Entities)
            {
                if (entity == null) continue;

                var exploded = ExplodeAndTransform(
                    entity,
                    transform,
                    dimensionColor,
                    dimensionLayer,
                    dimensionLineWeight
                );
                list.AddRange(exploded);
            }

            return list;
        }

        private static List<Entity> ExplodeMultiLeader(
            MultiLeader mleader,
            Transformation transform,
            ACadSharp.Color? parentColor = null,
            ACadSharp.Tables.Layer? parentLayer = null,
            LineWeightType? parentLineWeight = null)
        {
            var list = new List<Entity>();
            if (mleader == null || mleader.ContextData == null)
            {
                return list;
            }

            ACadSharp.Color? mleaderColor = mleader.Color;
            if (mleaderColor == null) mleaderColor = ACadSharp.Color.ByLayer;
            if (mleaderColor.Value.IsByBlock && parentColor != null) mleaderColor = parentColor;

            ACadSharp.Tables.Layer mleaderLayer = mleader.Layer;
            if (mleaderLayer != null && mleaderLayer.Name == "0" && parentLayer != null) mleaderLayer = parentLayer;

            LineWeightType mleaderLineWeight = mleader.LineWeight;
            if (mleaderLineWeight == LineWeightType.ByBlock && parentLineWeight != null)
            {
                mleaderLineWeight = parentLineWeight.Value;
            }

            // 1. Leader Lines
            if (mleader.ContextData.LeaderRoots != null)
            {
                foreach (var root in mleader.ContextData.LeaderRoots)
                {
                    if (root == null || root.Lines == null) continue;

                    foreach (var line in root.Lines)
                    {
                        if (line == null || line.Points == null || line.Points.Count < 2) continue;

                        for (int i = 1; i < line.Points.Count; i++)
                        {
                            var segment = new Line()
                            {
                                StartPoint = transform.TransformPoint(line.Points[i - 1]),
                                EndPoint = transform.TransformPoint(line.Points[i]),
                                Color = mleaderColor.Value,
                                Layer = mleaderLayer,
                                LineType = mleader.LineType,
                                LineWeight = mleaderLineWeight
                            };
                            list.Add(segment);
                        }

                        var p0 = line.Points[0];
                        var p1 = line.Points[1];
                        double dx = p1.X - p0.X;
                        double dy = p1.Y - p0.Y;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len > 0)
                        {
                            double vx = dx / len;
                            double vy = dy / len;

                            double arrowSize = line.ArrowheadSize > 0 ? line.ArrowheadSize : 1.5;

                            double cos30 = Math.Cos(Math.PI / 6.0);
                            double sin30 = Math.Sin(Math.PI / 6.0);

                            double w1x = vx * cos30 - vy * sin30;
                            double w1y = vx * sin30 + vy * cos30;

                            double w2x = vx * cos30 - vy * (-sin30);
                            double w2y = vx * (-sin30) + vy * cos30;

                            var wingEnd1 = new XYZ(p0.X + w1x * arrowSize, p0.Y + w1y * arrowSize, p0.Z);
                            var wingEnd2 = new XYZ(p0.X + w2x * arrowSize, p0.Y + w2y * arrowSize, p0.Z);

                            var arrowLine1 = new Line()
                            {
                                StartPoint = transform.TransformPoint(p0),
                                EndPoint = transform.TransformPoint(wingEnd1),
                                Color = mleaderColor.Value,
                                Layer = mleaderLayer,
                                LineType = mleader.LineType,
                                LineWeight = mleaderLineWeight
                            };

                            var arrowLine2 = new Line()
                            {
                                StartPoint = transform.TransformPoint(p0),
                                EndPoint = transform.TransformPoint(wingEnd2),
                                Color = mleaderColor.Value,
                                Layer = mleaderLayer,
                                LineType = mleader.LineType,
                                LineWeight = mleaderLineWeight
                            };

                            list.Add(arrowLine1);
                            list.Add(arrowLine2);
                        }
                    }
                }
            }

            // 2. Content
            if (mleader.ContentType == LeaderContentType.MText && !string.IsNullOrEmpty(mleader.ContextData.TextLabel))
            {
                var mtext = new MText()
                {
                    Value = mleader.ContextData.TextLabel,
                    InsertPoint = mleader.ContextData.TextLocation,
                    Height = mleader.ContextData.TextHeight > 0 ? mleader.ContextData.TextHeight : 1.0,
                    AlignmentPoint = new XYZ(Math.Cos(mleader.ContextData.TextRotation), Math.Sin(mleader.ContextData.TextRotation), 0.0),
                    Color = mleaderColor.Value,
                    Layer = mleaderLayer,
                    LineType = mleader.LineType,
                    LineWeight = mleaderLineWeight
                };
                var exploded = ExplodeAndTransform(mtext, transform, mleaderColor, mleaderLayer, mleaderLineWeight);
                list.AddRange(exploded);
            }
            else if (mleader.ContentType == LeaderContentType.Block && mleader.ContextData.BlockContent != null)
            {
                var insertBlock = new Insert(mleader.ContextData.BlockContent)
                {
                    InsertPoint = mleader.ContextData.BlockContentLocation,
                    XScale = mleader.ContextData.BlockContentScale.X,
                    YScale = mleader.ContextData.BlockContentScale.Y,
                    ZScale = mleader.ContextData.BlockContentScale.Z,
                    Rotation = mleader.ContextData.BlockContentRotation,
                    Color = mleaderColor.Value,
                    Layer = mleaderLayer,
                    LineType = mleader.LineType,
                    LineWeight = mleaderLineWeight
                };

                var mleaderAttribDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (mleader.BlockAttributes != null)
                {
                    foreach (var attr in mleader.BlockAttributes)
                    {
                        if (attr != null && attr.AttributeDefinition != null)
                        {
                            string tag = attr.AttributeDefinition.Tag ?? "";
                            if (!string.IsNullOrEmpty(tag))
                            {
                                mleaderAttribDict[tag] = attr.Text ?? "";
                            }
                        }
                    }
                }

                var exploded = ExplodeAndTransform(insertBlock, transform, mleaderColor, mleaderLayer, mleaderLineWeight, mleaderAttribDict);
                list.AddRange(exploded);
            }

            return list;
        }
    }
}
