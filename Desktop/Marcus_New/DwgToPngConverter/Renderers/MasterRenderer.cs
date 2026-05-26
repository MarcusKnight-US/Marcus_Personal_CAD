using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using ACadSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class MasterRenderer
    {
        private readonly Dictionary<Type, IEntityRenderer> _rendererMap = new();
        private readonly List<IEntityRenderer> _rendererFallback = new();
        private readonly Dictionary<Type, IEntityRenderer?> _resolvedRendererCache = new();

        public float OverallLineWeight { get; set; } = 4f;
        public string BackgroundColorHex { get; set; } = "#FFFFFF";

        public MasterRenderer()
        {
            RegisterRenderer(new LineRenderer());
            RegisterRenderer(new CircleRenderer());
            RegisterRenderer(new ArcRenderer());
            RegisterRenderer(new PolylineRenderer());
            RegisterRenderer(new SplineRenderer());
            RegisterRenderer(new EllipseRenderer());
            RegisterRenderer(new TextEntityRenderer());
            RegisterRenderer(new MTextRenderer());
            RegisterRenderer(new SolidRenderer());
            RegisterRenderer(new RasterImageRenderer());
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

        public void RenderAll(List<Entity> entities, BoundingBox bbox, string outputPath, string? dwgFilePath = null)
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

            int width = 1000;
            int height = 1000;

            float scaleX = width / (float)bbox.Width;
            float scaleY = height / (float)bbox.Height;
            float scale = Math.Min(scaleX, scaleY) * 0.9f;
            float offsetX = (width - (float)bbox.Width * scale) / 2f;
            float offsetY = (height - (float)bbox.Height * scale) / 2f;

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

            var context = new RenderContext(canvas, bbox, scale, offsetX, offsetY, height, paint, dwgFilePath);

            foreach (var entity in entities)
            {
                if (entity == null)
                {
                    continue;
                }

                paint.Color = ResolveSKColor(entity, backgroundColor);
                
                float resolvedWeight = ResolveLineWeightValue(entity);
                paint.StrokeWidth = Math.Max(0.5f, OverallLineWeight * (resolvedWeight / 25f));

                var renderer = FindRenderer(entity);
                renderer?.Draw(context, entity);
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
    }
}
