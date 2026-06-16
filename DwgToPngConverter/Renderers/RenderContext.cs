using System;
using SkiaSharp;
using ACadSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public readonly struct RenderContext
    {
        public SKCanvas Canvas { get; }
        public BoundingBox BoundingBox { get; }
        public float Scale { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public int Height { get; }
        public SKPaint Paint { get; }
        public RenderResourceCache ResourceCache { get; }
        public string? DwgFilePath { get; }
        public Viewport? ActiveViewport { get; }
        public float TextMultiplier { get; }

        public Transformation CurrentTransformation { get; }
        public ACadSharp.Color? OverrideColor { get; }
        public ACadSharp.Tables.Layer? OverrideLayer { get; }
        public LineWeightType? OverrideLineWeight { get; }
        public System.Collections.Generic.Dictionary<string, string>? AttributeValues { get; }

        public Transformation ActiveTransformation => 
            (CurrentTransformation.M11 == 0 && CurrentTransformation.M12 == 0 && CurrentTransformation.M21 == 0 && CurrentTransformation.M22 == 0)
            ? Transformation.Identity
            : CurrentTransformation;

        public float TransformationScale => (float)Math.Max(Math.Abs(ActiveTransformation.ScaleX), Math.Abs(ActiveTransformation.ScaleY));
        public float EffectiveScale => Scale * (ActiveViewport != null ? (float)ActiveViewport.ScaleFactor : 1f) * TransformationScale;

        public RenderContext(
            SKCanvas canvas, BoundingBox bbox, float scale, float offsetX, float offsetY, int height, SKPaint paint, RenderResourceCache resourceCache, string? dwgFilePath, Viewport? activeViewport, float textMultiplier,
            Transformation currentTransformation, ACadSharp.Color? overrideColor, ACadSharp.Tables.Layer? overrideLayer, LineWeightType? overrideLineWeight, System.Collections.Generic.Dictionary<string, string>? attributeValues)
        {
            Canvas = canvas;
            BoundingBox = bbox;
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Height = height;
            Paint = paint;
            ResourceCache = resourceCache;
            DwgFilePath = dwgFilePath;
            ActiveViewport = activeViewport;
            TextMultiplier = textMultiplier;
            CurrentTransformation = currentTransformation;
            OverrideColor = overrideColor;
            OverrideLayer = overrideLayer;
            OverrideLineWeight = overrideLineWeight;
            AttributeValues = attributeValues;
        }

        public RenderContext(SKCanvas canvas, BoundingBox bbox, float scale, float offsetX, float offsetY, int height, SKPaint paint, RenderResourceCache resourceCache, string? dwgFilePath = null, Viewport? activeViewport = null, float textMultiplier = 1f)
            : this(canvas, bbox, scale, offsetX, offsetY, height, paint, resourceCache, dwgFilePath, activeViewport, textMultiplier, Transformation.Identity, null, null, null, null)
        {
        }

        public SKPoint ToScreenPoint(double x, double y)
        {
            var localPt = new CSMath.XYZ(x, y, 0);
            var transformed = ActiveTransformation.TransformPoint(localPt);
            x = transformed.X;
            y = transformed.Y;

            if (ActiveViewport != null)
            {
                var vp = ActiveViewport;
                double dx = x - vp.ViewTarget.X;
                double dy = y - vp.ViewTarget.Y;
                double px = vp.Center.X + (dx - vp.ViewCenter.X) * vp.ScaleFactor;
                double py = vp.Center.Y + (dy - vp.ViewCenter.Y) * vp.ScaleFactor;
                return new SKPoint(
                    TransformService.TransformX(px, BoundingBox.MinX, Scale, OffsetX),
                    TransformService.TransformY(py, BoundingBox.MinY, Scale, OffsetY, Height)
                );
            }
            return new SKPoint(
                TransformService.TransformX(x, BoundingBox.MinX, Scale, OffsetX),
                TransformService.TransformY(y, BoundingBox.MinY, Scale, OffsetY, Height)
            );
        }

        public SKPoint ToScreenPoint(CSMath.XY point)
        {
            return ToScreenPoint(point.X, point.Y);
        }

        public SKPoint ToScreenPoint(CSMath.XYZ point)
        {
            return ToScreenPoint(point.X, point.Y);
        }

        public RenderContext WithTransformationAndOverrides(
            Transformation transformation,
            ACadSharp.Color? overrideColor,
            ACadSharp.Tables.Layer? overrideLayer,
            LineWeightType? overrideLineWeight,
            System.Collections.Generic.Dictionary<string, string>? attributeValues)
        {
            return new RenderContext(
                Canvas, BoundingBox, Scale, OffsetX, OffsetY, Height, Paint, ResourceCache, DwgFilePath, ActiveViewport, TextMultiplier,
                transformation, overrideColor, overrideLayer, overrideLineWeight, attributeValues
            );
        }

        public RenderContext WithTransformation(Transformation transformation)
        {
            return new RenderContext(
                Canvas, BoundingBox, Scale, OffsetX, OffsetY, Height, Paint, ResourceCache, DwgFilePath, ActiveViewport, TextMultiplier,
                transformation, OverrideColor, OverrideLayer, OverrideLineWeight, AttributeValues
            );
        }
    }
}
