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

            var context = new RenderContext(canvas, bbox, scale, offsetX, offsetY, height, paint, dwgFilePath, activeViewport: null, textMultiplier: AppConfig.Instance.TextSizeMultiplier);

            foreach (var entity in entities)
            {
                if (entity == null)
                {
                    continue;
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();

                paint.Color = ResolveSKColor(entity, backgroundColor);
                
                float resolvedWeight = ResolveLineWeightValue(entity);
                paint.StrokeWidth = Math.Max(AppConfig.Instance.MinLineWeight, OverallLineWeight * (resolvedWeight / 25f));

                paint.PathEffect = CreatePathEffect(entity, context.EffectiveScale);

                var renderer = FindRenderer(entity);
                renderer?.Draw(context, entity);

                paint.PathEffect = null;

                sw.Stop();
                PerformanceTracker.RecordRender(entity.GetType().Name, sw.Elapsed.TotalMilliseconds);
            }

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
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

        private static float ResolveLineWeightValue(Entity entity)
        {
            LineWeightType lineWeight = entity.LineWeight;

            if (lineWeight == LineWeightType.ByLayer)
            {
                if (entity.Layer != null)
                {
                    lineWeight = entity.Layer.LineWeight;
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

        private static SKColor ResolveSKColor(Entity entity, SKColor backgroundColor)
        {
            float bgBrightness = (backgroundColor.Red * 0.299f + backgroundColor.Green * 0.587f + backgroundColor.Blue * 0.114f) / 255f;
            SKColor defaultColor = bgBrightness < 0.5f ? SKColors.White : SKColors.Black;
            SKColor resultColor = defaultColor;

            ACadSharp.Color? color = entity.Color;
            if (color == null || color.Value.IsByLayer)
            {
                if (entity.Layer != null)
                {
                    color = entity.Layer.Color;
                }
            }

            if (color != null)
            {
                var c = color.Value;
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
            var paperContext = new RenderContext(canvas, paperBBox, scale, offsetX, offsetY, height, paint, dwgFilePath, activeViewport: null, textMultiplier: AppConfig.Instance.TextSizeMultiplier);

            // 6. Draw all paper space entities (excluding viewports Id > 1 which are drawn separately)
            foreach (var entity in paperScene.Entities)
            {
                if (entity == null || (entity is Viewport vp && vp.Id > 1))
                {
                    continue;
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();

                paint.Color = ResolveSKColor(entity, backgroundColor);
                float resolvedWeight = ResolveLineWeightValue(entity);
                paint.StrokeWidth = Math.Max(AppConfig.Instance.MinLineWeight, OverallLineWeight * (resolvedWeight / 25f));

                paint.PathEffect = CreatePathEffect(entity, paperContext.EffectiveScale);

                var renderer = FindRenderer(entity);
                renderer?.Draw(paperContext, entity);

                paint.PathEffect = null;

                sw.Stop();
                PerformanceTracker.RecordRender(entity.GetType().Name, sw.Elapsed.TotalMilliseconds);
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
                        using var borderPaint = new SKPaint
                        {
                            Color = ResolveSKColor(vp, backgroundColor),
                            StrokeWidth = Math.Max(AppConfig.Instance.MinLineWeight, OverallLineWeight * (ResolveLineWeightValue(vp) / 25f)),
                            Style = SKPaintStyle.Stroke,
                            IsAntialias = true
                        };
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
                            screenClipRect, paint, dwgFilePath);
                    }
                    else
                    {
                        modelContext = new RenderContext(canvas, paperBBox, scale, offsetX, offsetY, height, paint, dwgFilePath, vp, AppConfig.Instance.TextSizeMultiplier);
                    }

                    // 7c. Clip and render model space entities
                    canvas.Save();
                    canvas.ClipRect(screenClipRect);

                    foreach (var mEntity in modelScene.Entities)
                    {
                        if (mEntity == null) continue;

                        var sw = System.Diagnostics.Stopwatch.StartNew();

                        paint.Color = ResolveSKColor(mEntity, backgroundColor);
                        float resolvedWeight = ResolveLineWeightValue(mEntity);
                        paint.StrokeWidth = Math.Max(AppConfig.Instance.MinLineWeight, OverallLineWeight * (resolvedWeight / 25f));

                        paint.PathEffect = CreatePathEffect(mEntity, modelContext.EffectiveScale);

                        var renderer = FindRenderer(mEntity);
                        renderer?.Draw(modelContext, mEntity);

                            paint.PathEffect = null;

                        sw.Stop();
                        PerformanceTracker.RecordRender(mEntity.GetType().Name, sw.Elapsed.TotalMilliseconds);
                    }

                    canvas.Restore();
                }
            }

            // 8. Save snapshot
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
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
            SKRect screenRect, SKPaint paint, string? dwgFilePath)
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
                canvasHeight, paint, dwgFilePath, activeViewport: null, textMultiplier: AppConfig.Instance.TextSizeMultiplier);
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
            if (ltype == null)
            {
                return null;
            }
            return DashedPathEffect;
        }
    }
}
