using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Objects;
using DwgToPngConverter.Geometry;
using DwgToPngConverter;
using CSMath;
using BoundingBox = DwgToPngConverter.Geometry.BoundingBox;

namespace DwgToPngConverter.Renderers
{
    public class MasterRenderer
    {
        private static readonly float[] DashIntervals = new float[] { 12f, 12f };
        private static readonly SKPathEffect? DashedPathEffect = SKPathEffect.CreateDash(DashIntervals, 0f);

        private readonly Dictionary<Type, IEntityRenderer> _rendererMap = new();
        private readonly List<IEntityRenderer> _rendererFallback = new();
        private readonly Dictionary<Type, IEntityRenderer?> _resolvedRendererCache = new();

        public float OverallLineWeight { get; set; } = AppConfig.Instance.OverallLineWeight;
        public string BackgroundColorHex { get; set; } = AppConfig.Instance.BackgroundColor;

        public MasterRenderer()
        {
            RegisterRenderer(new LineRenderer());
            RegisterRenderer(new ArcRenderer());
            RegisterRenderer(new CircleRenderer());
            RegisterRenderer(new PolylineRenderer());
            RegisterRenderer(new SplineRenderer());
            RegisterRenderer(new EllipseRenderer());
            RegisterRenderer(new TextEntityRenderer());
            RegisterRenderer(new MTextRenderer());
            RegisterRenderer(new SolidRenderer());
            RegisterRenderer(new RasterImageRenderer());
            RegisterRenderer(new HatchRenderer());
            RegisterRenderer(new PointRenderer());
        }

        public void RegisterRenderer(IEntityRenderer renderer)
        {
            if (renderer == null) return;
            _rendererMap[renderer.EntityType] = renderer;

            if (!_rendererFallback.Contains(renderer))
            {
                _rendererFallback.Add(renderer);
            }

            _resolvedRendererCache.Clear();
        }

        public void RenderAll(List<Entity> entities, BoundingBox bbox, string outputPath, string? dwgFilePath = null, ConversionDebugInfo? debugInfo = null)
        {
            if (entities == null || entities.Count == 0)
            {
                Console.WriteLine("Warning: No entities to render.");
                return;
            }

            if (bbox.IsEmpty || bbox.Width == 0 || bbox.Height == 0)
            {
                Console.WriteLine("Warning: Bounding box has zero width or height. No entities to render.");
                return;
            }

            int width = AppConfig.Instance.ModelSpaceWidth;
            int height = AppConfig.Instance.ModelSpaceHeight;

            float scaleX = width / (float)bbox.Width;
            float scaleY = height / (float)bbox.Height;
            float scale = Math.Min(scaleX, scaleY) * AppConfig.Instance.ModelSpaceMarginMultiplier;
            float offsetX = (width - (float)bbox.Width * scale) / 2f;
            float offsetY = (height - (float)bbox.Height * scale) / 2f;

            if (debugInfo != null)
            {
                debugInfo.ImageWidth = width;
                debugInfo.ImageHeight = height;
                debugInfo.BBoxMinX = bbox.MinX;
                debugInfo.BBoxMinY = bbox.MinY;
                debugInfo.BBoxMaxX = bbox.MaxX;
                debugInfo.BBoxMaxY = bbox.MaxY;
                debugInfo.BBoxWidth = bbox.Width;
                debugInfo.BBoxHeight = bbox.Height;
                debugInfo.ScaleFactor = scale;
                debugInfo.OffsetX = offsetX;
                debugInfo.OffsetY = offsetY;
                debugInfo.TotalEntities = entities.Count;
                foreach (var entity in entities)
                {
                    if (entity == null) continue;
                    string typeName = entity.GetType().Name;
                    if (!debugInfo.EntityCounts.ContainsKey(typeName))
                        debugInfo.EntityCounts[typeName] = 0;
                    debugInfo.EntityCounts[typeName]++;
                }
            }

            SKColor backgroundColor = SKColors.White;
            if (!string.IsNullOrWhiteSpace(BackgroundColorHex))
            {
                if (TryParseHexColor(BackgroundColorHex, out var parsedColor))
                {
                    backgroundColor = parsedColor;
                }
                else
                {
                    Console.WriteLine($"Warning: Invalid background color hex '{BackgroundColorHex}', falling back to white.");
                }
            }

            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(backgroundColor);

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = OverallLineWeight,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            using var cache = new RenderResourceCache();
            var context = new RenderContext(canvas, bbox, scale, offsetX, offsetY, height, paint, cache, dwgFilePath, activeViewport: null, textMultiplier: AppConfig.Instance.TextSizeMultiplier);

            foreach (var entity in entities)
            {
                RenderEntity(context, entity, backgroundColor);
            }

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, AppConfig.Instance.CompressionQuality);
            using var stream = File.OpenWrite(outputPath);
            data.SaveTo(stream);
        }

        private IEntityRenderer? FindRenderer(Entity entity)
        {
            if (entity == null) return null;
            Type entityType = entity.GetType();

            if (_resolvedRendererCache.TryGetValue(entityType, out var cachedRenderer))
            {
                return cachedRenderer;
            }

            if (_rendererMap.TryGetValue(entityType, out var renderer))
            {
                _resolvedRendererCache[entityType] = renderer;
                return renderer;
            }

            var fallbackRenderer = _rendererFallback.FirstOrDefault(r => r.EntityType.IsAssignableFrom(entityType));
            _resolvedRendererCache[entityType] = fallbackRenderer;
            return fallbackRenderer;
        }

        private static float ResolveLineWeightValue(Entity entity, ACadSharp.Tables.Layer? overrideLayer = null, LineWeightType? overrideLineWeight = null)
        {
            LineWeightType lineWeight = entity.LineWeight;

            if (lineWeight == LineWeightType.ByBlock && overrideLineWeight != null)
            {
                lineWeight = overrideLineWeight.Value;
            }

            if (lineWeight == LineWeightType.ByLayer)
            {
                var layer = entity.Layer;
                if (layer != null && layer.Name == "0" && overrideLayer != null)
                {
                    layer = overrideLayer;
                }
                if (layer != null)
                {
                    lineWeight = layer.LineWeight;
                }
            }

            switch (lineWeight)
            {
                case LineWeightType.ByLayer:
                case LineWeightType.ByBlock:
                case LineWeightType.Default:
                case LineWeightType.ByDIPs:
                    return 25f; // Default standard AutoCAD lineweight is 0.25 mm (W25)
                default:
                    short val = (short)lineWeight;
                    return val >= 0 ? val : 25f;
            }
        }

        private static SKColor ResolveSKColor(Entity entity, SKColor backgroundColor, ACadSharp.Color? overrideColor = null, ACadSharp.Tables.Layer? overrideLayer = null)
        {
            float bgBrightness = (backgroundColor.Red * 0.299f + backgroundColor.Green * 0.587f + backgroundColor.Blue * 0.114f) / 255f;
            SKColor defaultColor = bgBrightness < 0.5f ? SKColors.White : SKColors.Black;
            SKColor resultColor = defaultColor;

            ACadSharp.Color? color = entity.Color;
            if (color == null || color.Value.IsByLayer)
            {
                var layer = entity.Layer;
                if (layer != null && layer.Name == "0" && overrideLayer != null)
                {
                    layer = overrideLayer;
                }
                if (layer != null)
                {
                    color = layer.Color;
                }
            }

            if (color != null)
            {
                var c = color.Value;
                if (c.IsByBlock && overrideColor != null)
                {
                    c = overrideColor.Value;
                }

                if (c.IsTrueColor)
                {
                    resultColor = new SKColor(c.R, c.G, c.B);
                }
                else if (c.IsByBlock || c.IsByLayer)
                {
                    resultColor = defaultColor;
                }
                else
                {
                    int index = c.Index;
                    if (index >= 1 && index <= 255)
                    {
                        if (index == 7)
                        {
                            resultColor = defaultColor;
                        }
                        else
                        {
                            try
                            {
                                ReadOnlySpan<byte> rgb = ACadSharp.Color.GetIndexRGB((byte)index);
                                resultColor = new SKColor(rgb[0], rgb[1], rgb[2]);
                            }
                            catch
                            {
                                resultColor = defaultColor;
                            }
                        }
                    }
                }
            }

            return resultColor;
        }

        public void RenderLayout(CadDocument doc, Layout layout, string outputPath, string? dwgFilePath = null, ConversionDebugInfo? debugInfo = null)
        {
            DwgToPngConverter.Scene.CadScene.SheetNumber = null;

            if (layout == null || layout.AssociatedBlock == null)
            {
                Console.WriteLine("Warning: Invalid layout or layout associated block is null.");
                return;
            }

            // 1. Explode paper space entities
            var paperScene = new DwgToPngConverter.Scene.CadScene();
            paperScene.AddEntities(layout.AssociatedBlock.Entities);

            // 2. Explode model space entities
            var modelScene = new DwgToPngConverter.Scene.CadScene();
            modelScene.AddEntities(doc.Entities);

            // 3. Find paper bounds via Viewport with Id == 1
            BoundingBox? paperBBox = null;
            var mainVp = paperScene.Entities.OfType<Viewport>().FirstOrDefault(v => v.Id == 1);
            if (mainVp != null)
            {
                paperBBox = new BoundingBox();
                paperBBox.MinX = mainVp.Center.X - mainVp.Width / 2;
                paperBBox.MaxX = mainVp.Center.X + mainVp.Width / 2;
                paperBBox.MinY = mainVp.Center.Y - mainVp.Height / 2;
                paperBBox.MaxY = mainVp.Center.Y + mainVp.Height / 2;
            }
            else
            {
                paperBBox = paperScene.BoundingBox;
            }

            if (paperBBox.IsEmpty || paperBBox.Width == 0 || paperBBox.Height == 0)
            {
                Console.WriteLine("Warning: Paper space bounding box has zero width or height.");
                return;
            }

            // 4. Set size based on configuration, paper size and target DPI
            int width = 2400;
            var config = AppConfig.Instance;
            double pWidthMm = layout.PaperWidth > 0 ? layout.PaperWidth : config.DefaultPaperWidthMm;
            double pHeightMm = layout.PaperHeight > 0 ? layout.PaperHeight : config.DefaultPaperHeightMm;

            double pWidthInches = pWidthMm / 25.4;
            double pHeightInches = pHeightMm / 25.4;

            bool bboxIsLandscape = paperBBox.Width > paperBBox.Height;
            bool paperIsLandscape = pWidthInches > pHeightInches;

            if (bboxIsLandscape != paperIsLandscape)
            {
                double temp = pWidthInches;
                pWidthInches = pHeightInches;
                pHeightInches = temp;
            }

            width = (int)Math.Round(pWidthInches * config.DefaultDpi);
            if (width < config.MinLayoutWidth) width = config.MinLayoutWidth;
            if (width > config.MaxLayoutWidth) width = config.MaxLayoutWidth;

            int height = (int)Math.Round(width * (paperBBox.Height / paperBBox.Width));

            float scaleX = width / (float)paperBBox.Width;
            float scaleY = height / (float)paperBBox.Height;
            float scale = Math.Min(scaleX, scaleY) * AppConfig.Instance.PaperSpaceMarginMultiplier;
            float offsetX = (width - (float)paperBBox.Width * scale) / 2f;
            float offsetY = (height - (float)paperBBox.Height * scale) / 2f;

            if (debugInfo != null)
            {
                debugInfo.LayoutName = layout.Name;
                debugInfo.ImageWidth = width;
                debugInfo.ImageHeight = height;
                debugInfo.BBoxMinX = paperBBox.MinX;
                debugInfo.BBoxMinY = paperBBox.MinY;
                debugInfo.BBoxMaxX = paperBBox.MaxX;
                debugInfo.BBoxMaxY = paperBBox.MaxY;
                debugInfo.BBoxWidth = paperBBox.Width;
                debugInfo.BBoxHeight = paperBBox.Height;
                debugInfo.ScaleFactor = scale;
                debugInfo.OffsetX = offsetX;
                debugInfo.OffsetY = offsetY;
                
                int totalEnts = paperScene.Entities.Count + modelScene.Entities.Count;
                debugInfo.TotalEntities = totalEnts;
                
                foreach (var entity in paperScene.Entities)
                {
                    if (entity == null) continue;
                    string typeName = "Paper:" + entity.GetType().Name;
                    if (!debugInfo.EntityCounts.ContainsKey(typeName))
                        debugInfo.EntityCounts[typeName] = 0;
                    debugInfo.EntityCounts[typeName]++;
                }
                foreach (var entity in modelScene.Entities)
                {
                    if (entity == null) continue;
                    string typeName = "Model:" + entity.GetType().Name;
                    if (!debugInfo.EntityCounts.ContainsKey(typeName))
                        debugInfo.EntityCounts[typeName] = 0;
                    debugInfo.EntityCounts[typeName]++;
                }
            }

            // 5. Initialize Skia canvas with white background
            SKColor backgroundColor = SKColors.White;
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(backgroundColor);

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = OverallLineWeight,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            // Set up RenderContext for paper space entities
            using var cache = new RenderResourceCache();
            var paperContext = new RenderContext(canvas, paperBBox, scale, offsetX, offsetY, height, paint, cache, dwgFilePath, activeViewport: null, textMultiplier: AppConfig.Instance.TextSizeMultiplier);

            // 6. Draw all paper space entities (excluding viewports Id > 1 which are drawn separately)
            foreach (var entity in paperScene.Entities)
            {
                if (entity == null || (entity is Viewport vp && vp.Id > 1))
                {
                    continue;
                }

                RenderEntity(paperContext, entity, backgroundColor);
            }

            // 7. Render Model Space inside each Viewport (Id > 1)
            foreach (var entity in paperScene.Entities)
            {
                if (entity is Viewport vp && vp.Id > 1)
                {
                    // Viewport screen clip bounds
                    SKPoint screenPMin = paperContext.ToScreenPoint(vp.Center.X - vp.Width / 2, vp.Center.Y - vp.Height / 2);
                    SKPoint screenPMax = paperContext.ToScreenPoint(vp.Center.X + vp.Width / 2, vp.Center.Y + vp.Height / 2);

                    float cMinX = Math.Min(screenPMin.X, screenPMax.X);
                    float cMaxX = Math.Max(screenPMin.X, screenPMax.X);
                    float cMinY = Math.Min(screenPMin.Y, screenPMax.Y);
                    float cMaxY = Math.Max(screenPMin.Y, screenPMax.Y);

                    var screenClipRect = new SKRect(cMinX, cMinY, cMaxX, cMaxY);

                    // 7a. Draw Viewport Boundary (if viewport outline is visible)
                    bool borderIsVisible = !vp.IsInvisible;
                    if (vp.Layer != null && !vp.Layer.IsOn)
                        borderIsVisible = false;

                    if (borderIsVisible)
                    {
                        var borderPaint = paperContext.ResourceCache.GetPaint(
                            ResolveSKColor(vp, backgroundColor),
                            SKPaintStyle.Stroke,
                            isAntialias: true,
                            strokeWidth: Math.Max(AppConfig.Instance.MinLineWeight, OverallLineWeight * (ResolveLineWeightValue(vp) / 25f))
                        );
                        canvas.DrawRect(screenClipRect, borderPaint);
                    }

                    // 7b. Determine the model-space context for this viewport.
                    //     Check whether the viewport's view window overlaps the model bounding box.
                    //     If it doesn't (ViewCenter/ViewTarget not in model-space units), fall back to
                    //     a zoom-to-extents render that maps the whole model bbox into the viewport rect.
                    RenderContext modelContext;
                    if (modelScene.BoundingBox.IsEmpty)
                    {
                        canvas.Save();
                        canvas.Restore();
                        continue;
                    }

                    bool useZoomToExtents = !ViewportOverlapsModelEntities(vp, modelScene.Entities);
                    if (useZoomToExtents)
                    {
                        // Build a context that maps the model bbox into the screen clip rect directly.
                        modelContext = BuildZoomToExtentsContext(canvas, modelScene.BoundingBox,
                            screenClipRect, paint, paperContext.ResourceCache, dwgFilePath);
                    }
                    else
                    {
                        modelContext = new RenderContext(canvas, paperBBox, scale, offsetX, offsetY, height, paint, paperContext.ResourceCache, dwgFilePath, vp, AppConfig.Instance.TextSizeMultiplier);
                    }

                    // 7c. Clip and render model space entities
                    canvas.Save();
                    canvas.ClipRect(screenClipRect);

                    foreach (var mEntity in modelScene.Entities)
                    {
                        RenderEntity(modelContext, mEntity, backgroundColor);
                    }

                    canvas.Restore();
                }
            }

            // 8. Save snapshot
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, AppConfig.Instance.CompressionQuality);
            using var stream = File.OpenWrite(outputPath);
            data.SaveTo(stream);
        }

        /// <summary>
        /// Returns true if the viewport's view window (in model coordinates) overlaps the bounding box of any individual entity.
        /// ViewCenter is the model-space point at the center of the viewport; ViewTarget offsets the DCS origin.
        /// </summary>
        private static bool ViewportOverlapsModelEntities(Viewport vp, IEnumerable<Entity> entities)
        {
            if (vp.ScaleFactor <= 0) return false;

            // Compute the model-space rectangle visible through this viewport
            double modelCenterX = vp.ViewTarget.X + vp.ViewCenter.X;
            double modelCenterY = vp.ViewTarget.Y + vp.ViewCenter.Y;
            double halfW = (vp.Width  / 2.0) / vp.ScaleFactor;
            double halfH = (vp.Height / 2.0) / vp.ScaleFactor;

            double vpMinX = modelCenterX - halfW;
            double vpMaxX = modelCenterX + halfW;
            double vpMinY = modelCenterY - halfH;
            double vpMaxY = modelCenterY + halfH;

            foreach (var entity in entities)
            {
                if (entity == null) continue;
                if (!ExtentsCalculator.TryGetExtents(entity, out var extents))
                {
                    continue;
                }

                // Rectangle intersection test for individual entity extents
                bool intersect = vpMaxX >= extents.MinX && vpMinX <= extents.MaxX &&
                                 vpMaxY >= extents.MinY && vpMinY <= extents.MaxY;
                if (intersect)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Builds a RenderContext that fits the entire model bounding box into the given screen rectangle
        /// (zoom-to-extents fallback when the viewport camera doesn't align with model coordinates).
        /// </summary>
        private static RenderContext BuildZoomToExtentsContext(SKCanvas canvas, BoundingBox modelBBox,
            SKRect screenRect, SKPaint paint, RenderResourceCache cache, string? dwgFilePath)
        {
            float screenW = screenRect.Width;
            float screenH = screenRect.Height;

            float scaleX = screenW / (float)modelBBox.Width;
            float scaleY = screenH / (float)modelBBox.Height;
            float fitScale = Math.Min(scaleX, scaleY) * AppConfig.Instance.PaperSpaceMarginMultiplier;   // inner margin

            float offsetX = screenRect.Left + (screenW - (float)modelBBox.Width  * fitScale) / 2f;
            float offsetY = screenRect.Top  + (screenH - (float)modelBBox.Height * fitScale) / 2f;

            // We pass height = total canvas height so TransformY flips correctly
            int canvasHeight = (int)canvas.DeviceClipBounds.Height;
            if (canvasHeight <= 0) canvasHeight = (int)screenRect.Bottom;

            return new RenderContext(canvas, modelBBox, fitScale, offsetX, offsetY,
                canvasHeight, paint, cache, dwgFilePath, activeViewport: null, textMultiplier: AppConfig.Instance.TextSizeMultiplier);
        }

        public static bool TryParseHexColor(string hex, out SKColor color)
        {
            color = SKColors.White;
            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            hex = hex.Trim().TrimStart('#');

            // Handle short hex formats like FFF or 333
            if (hex.Length == 3)
            {
                string r = new string(hex[0], 2);
                string g = new string(hex[1], 2);
                string b = new string(hex[2], 2);
                hex = r + g + b;
            }
            else if (hex.Length == 4)
            {
                string r = new string(hex[0], 2);
                string g = new string(hex[1], 2);
                string b = new string(hex[2], 2);
                string a = new string(hex[3], 2);
                hex = r + g + b + a;
            }

            if (hex.Length == 6)
            {
                if (byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                    byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                    byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
                {
                    color = new SKColor(r, g, b);
                    return true;
                }
            }
            else if (hex.Length == 8)
            {
                if (byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                    byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                    byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b) &&
                    byte.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out byte a))
                {
                    color = new SKColor(r, g, b, a);
                    return true;
                }
            }

            // Fallback to SkiaSharp's native TryParse
            return SKColor.TryParse(hex, out color);
        }

        private static ACadSharp.Tables.LineType? ResolveLineType(Entity entity)
        {
            ACadSharp.Tables.LineType? ltype = entity.LineType;

            if (ltype == null || ltype.Name.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                if (entity.Layer != null)
                {
                    ltype = entity.Layer.LineType;
                }
            }

            if (ltype == null || 
                ltype.Name.Equals("ByLayer", StringComparison.OrdinalIgnoreCase) || 
                ltype.Name.Equals("ByBlock", StringComparison.OrdinalIgnoreCase) || 
                ltype.Name.Equals("Continuous", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return ltype;
        }

        private static SKPathEffect? CreatePathEffect(Entity entity, float scale)
        {
            var ltype = ResolveLineType(entity);
            return DashedPathEffect;
        }

        private void RenderEntity(RenderContext context, Entity entity, SKColor backgroundColor)
        {
            if (entity == null || entity.IsInvisible)
            {
                return;
            }

            // Resolve Layer "0" inheritance
            var resolvedLayer = entity.Layer;
            if (resolvedLayer != null && resolvedLayer.Name == "0" && context.OverrideLayer != null)
            {
                resolvedLayer = context.OverrideLayer;
            }

            if (resolvedLayer != null && !(entity is Viewport))
            {
                if (!resolvedLayer.IsOn || (resolvedLayer.Flags & ACadSharp.Tables.LayerFlags.Frozen) != ACadSharp.Tables.LayerFlags.None)
                {
                    return;
                }
            }

            if (entity is Insert insert)
            {
                RenderInsert(context, insert, backgroundColor);
            }
            else if (entity is Dimension dimension)
            {
                RenderDimension(context, dimension, backgroundColor);
            }
            else if (entity is MultiLeader mleader)
            {
                RenderMultiLeader(context, mleader, backgroundColor);
            }
            else
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var paint = context.Paint;
                paint.Color = ResolveSKColor(entity, backgroundColor, context.OverrideColor, context.OverrideLayer);

                float resolvedWeight = ResolveLineWeightValue(entity, context.OverrideLayer, context.OverrideLineWeight);
                paint.StrokeWidth = Math.Max(AppConfig.Instance.MinLineWeight, OverallLineWeight * (resolvedWeight / 25f));

                paint.PathEffect = CreatePathEffect(entity, context.EffectiveScale);

                var renderer = FindRenderer(entity);
                renderer?.Draw(context, entity);

                paint.PathEffect = null;

                sw.Stop();
                PerformanceTracker.RecordRender(entity.GetType().Name, sw.Elapsed.TotalMilliseconds);
            }
        }

        private void RenderInsert(RenderContext context, Insert insert, SKColor backgroundColor)
        {
            if (insert.Block == null) return;

            // Resolve overrides for the block contents
            ACadSharp.Color? childOverrideColor = insert.Color;
            if (childOverrideColor == null) childOverrideColor = ACadSharp.Color.ByLayer;
            if (childOverrideColor.Value.IsByBlock && context.OverrideColor != null)
            {
                childOverrideColor = context.OverrideColor;
            }

            ACadSharp.Tables.Layer? childOverrideLayer = insert.Layer;
            if (childOverrideLayer != null && childOverrideLayer.Name == "0" && context.OverrideLayer != null)
            {
                childOverrideLayer = context.OverrideLayer;
            }

            LineWeightType childOverrideLineWeight = insert.LineWeight;
            if (childOverrideLineWeight == LineWeightType.ByBlock && context.OverrideLineWeight != null)
            {
                childOverrideLineWeight = context.OverrideLineWeight.Value;
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
            if (context.AttributeValues != null)
            {
                foreach (var kvp in context.AttributeValues)
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
                        var localTransform = new Transformation(
                            insert.XScale, insert.YScale, insert.ZScale,
                            insert.Rotation,
                            insert.InsertPoint,
                            colSpacing, rowSpacing,
                            col, row
                        );

                        var combinedTransform = localTransform.Combine(context.ActiveTransformation);

                        var childContext = context.WithTransformationAndOverrides(
                            combinedTransform,
                            childOverrideColor,
                            childOverrideLayer,
                            childOverrideLineWeight,
                            attribDict
                        );

                        RenderEntity(childContext, entity, backgroundColor);
                    }
                }
            }
        }

        private void RenderDimension(RenderContext context, Dimension dimension, SKColor backgroundColor)
        {
            if (dimension == null || dimension.Block == null) return;

            ACadSharp.Color? dimensionColor = dimension.Color;
            if (dimensionColor == null) dimensionColor = ACadSharp.Color.ByLayer;
            if (dimensionColor.Value.IsByBlock && context.OverrideColor != null)
            {
                dimensionColor = context.OverrideColor;
            }

            ACadSharp.Tables.Layer dimensionLayer = dimension.Layer;
            if (dimensionLayer != null && dimensionLayer.Name == "0" && context.OverrideLayer != null)
            {
                dimensionLayer = context.OverrideLayer;
            }

            LineWeightType dimensionLineWeight = dimension.LineWeight;
            if (dimensionLineWeight == LineWeightType.ByBlock && context.OverrideLineWeight != null)
            {
                dimensionLineWeight = context.OverrideLineWeight.Value;
            }

            foreach (var entity in dimension.Block.Entities)
            {
                if (entity == null) continue;

                var childContext = context.WithTransformationAndOverrides(
                    context.CurrentTransformation,
                    dimensionColor,
                    dimensionLayer,
                    dimensionLineWeight,
                    context.AttributeValues
                );

                RenderEntity(childContext, entity, backgroundColor);
            }
        }

        private void RenderMultiLeader(RenderContext context, MultiLeader mleader, SKColor backgroundColor)
        {
            if (mleader == null || mleader.ContextData == null) return;

            ACadSharp.Color? mleaderColor = mleader.Color;
            if (mleaderColor == null) mleaderColor = ACadSharp.Color.ByLayer;
            if (mleaderColor.Value.IsByBlock && context.OverrideColor != null)
            {
                mleaderColor = context.OverrideColor;
            }

            ACadSharp.Tables.Layer mleaderLayer = mleader.Layer;
            if (mleaderLayer != null && mleaderLayer.Name == "0" && context.OverrideLayer != null)
            {
                mleaderLayer = context.OverrideLayer;
            }

            LineWeightType mleaderLineWeight = mleader.LineWeight;
            if (mleaderLineWeight == LineWeightType.ByBlock && context.OverrideLineWeight != null)
            {
                mleaderLineWeight = context.OverrideLineWeight.Value;
            }

            var paint = context.Paint;
            var resolvedColor = ResolveSKColor(mleader, backgroundColor, mleaderColor, mleaderLayer);
            paint.Color = resolvedColor;

            float resolvedWeight = ResolveLineWeightValue(mleader, context.OverrideLayer, mleaderLineWeight);
            paint.StrokeWidth = Math.Max(AppConfig.Instance.MinLineWeight, OverallLineWeight * (resolvedWeight / 25f));
            paint.PathEffect = CreatePathEffect(mleader, context.EffectiveScale);

            // 1. Leader Lines and Landing Lines
            if (mleader.ContextData.LeaderRoots != null)
            {
                foreach (var root in mleader.ContextData.LeaderRoots)
                {
                    if (root == null || root.Lines == null) continue;

                    var connectionPoint = root.ConnectionPoint;
                    var direction = root.Direction;
                    double landingDistance = root.LandingDistance;
                    var landingStart = new XYZ(
                        connectionPoint.X - direction.X * landingDistance,
                        connectionPoint.Y - direction.Y * landingDistance,
                        connectionPoint.Z - direction.Z * landingDistance
                    );

                    if (landingDistance > 0)
                    {
                        var start = context.ToScreenPoint(landingStart);
                        var end = context.ToScreenPoint(connectionPoint);
                        context.Canvas.DrawLine(start, end, paint);
                    }

                    foreach (var line in root.Lines)
                    {
                        if (line == null || line.Points == null || line.Points.Count == 0) continue;

                        for (int i = 1; i < line.Points.Count; i++)
                        {
                            var start = context.ToScreenPoint(line.Points[i - 1]);
                            var end = context.ToScreenPoint(line.Points[i]);
                            context.Canvas.DrawLine(start, end, paint);
                        }

                        var lastPt = line.Points[line.Points.Count - 1];
                        var connectStart = context.ToScreenPoint(lastPt);
                        var connectEnd = context.ToScreenPoint(landingStart);
                        context.Canvas.DrawLine(connectStart, connectEnd, paint);

                        var p0 = line.Points[0];
                        var p1 = line.Points.Count > 1 ? line.Points[1] : landingStart;
                        double dx = p1.X - p0.X;
                        double dy = p1.Y - p0.Y;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len > 0)
                        {
                            double vx = dx / len;
                            double vy = dy / len;

                            double arrowSize = 1.5;
                            if (line.ArrowheadSize > 0)
                                arrowSize = line.ArrowheadSize;
                            else if (mleader.ContextData.ArrowheadSize > 0)
                                arrowSize = mleader.ContextData.ArrowheadSize;

                            double cos30 = Math.Cos(Math.PI / 6.0);
                            double sin30 = Math.Sin(Math.PI / 6.0);

                            double w1x = vx * cos30 - vy * sin30;
                            double w1y = vx * sin30 + vy * cos30;

                            double w2x = vx * cos30 - vy * (-sin30);
                            double w2y = vx * (-sin30) + vy * cos30;

                            var wingEnd1 = new XYZ(p0.X + w1x * arrowSize, p0.Y + w1y * arrowSize, p0.Z);
                            var wingEnd2 = new XYZ(p0.X + w2x * arrowSize, p0.Y + w2y * arrowSize, p0.Z);

                            var p0Screen = context.ToScreenPoint(p0);
                            var w1Screen = context.ToScreenPoint(wingEnd1);
                            var w2Screen = context.ToScreenPoint(wingEnd2);

                            using var arrowheadPath = new SKPath();
                            arrowheadPath.MoveTo(p0Screen.X, p0Screen.Y);
                            arrowheadPath.LineTo(w1Screen.X, w1Screen.Y);
                            arrowheadPath.LineTo(w2Screen.X, w2Screen.Y);
                            arrowheadPath.Close();

                            var fillPaint = context.ResourceCache.GetPaint(resolvedColor, SKPaintStyle.Fill, isAntialias: true);
                            context.Canvas.DrawPath(arrowheadPath, fillPaint);
                        }
                    }
                }
            }

            paint.PathEffect = null;

            // 2. Content
            if (mleader.ContentType == LeaderContentType.MText && !string.IsNullOrEmpty(mleader.ContextData.TextLabel))
            {
                var attachment = AttachmentPointType.TopLeft;
                if (mleader.ContextData.TextAttachmentPoint == ACadSharp.TextAttachmentPointType.Center)
                    attachment = AttachmentPointType.TopCenter;
                else if (mleader.ContextData.TextAttachmentPoint == ACadSharp.TextAttachmentPointType.Right)
                    attachment = AttachmentPointType.TopRight;

                var mtext = new MText()
                {
                    Value = mleader.ContextData.TextLabel,
                    InsertPoint = mleader.ContextData.TextLocation,
                    Height = mleader.ContextData.TextHeight > 0 ? mleader.ContextData.TextHeight : 1.0,
                    AlignmentPoint = new XYZ(Math.Cos(mleader.ContextData.TextRotation), Math.Sin(mleader.ContextData.TextRotation), 0.0),
                    AttachmentPoint = attachment,
                    RectangleWidth = mleader.ContextData.BoundaryWidth,
                    Color = mleaderColor.Value,
                    Layer = mleaderLayer,
                    LineType = mleader.LineType,
                    LineWeight = mleaderLineWeight
                };

                var childContext = context.WithTransformationAndOverrides(
                    context.CurrentTransformation,
                    mleaderColor,
                    mleaderLayer,
                    mleaderLineWeight,
                    context.AttributeValues
                );
                RenderEntity(childContext, mtext, backgroundColor);
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

                var childContext = context.WithTransformationAndOverrides(
                    context.CurrentTransformation,
                    mleaderColor,
                    mleaderLayer,
                    mleaderLineWeight,
                    mleaderAttribDict
                );
                RenderEntity(childContext, insertBlock, backgroundColor);
            }
        }
    }
}
