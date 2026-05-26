using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class MasterRenderer
    {
        private readonly Dictionary<Type, IEntityRenderer> _rendererMap;
        private readonly IList<IEntityRenderer> _rendererFallback;

        public MasterRenderer()
        {
            var renderers = new IEntityRenderer[]
            {
                new LineRenderer(),
                new CircleRenderer(),
                new ArcRenderer(),
                new PolylineRenderer(),
                new SplineRenderer()
            };

            _rendererMap = renderers.ToDictionary(renderer => renderer.EntityType);
            _rendererFallback = renderers;
        }

        public void RenderAll(List<Entity> entities, BoundingBox bbox, string outputPath)
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
            int strokeWidth = 4;

            float scaleX = width / (float)bbox.Width;
            float scaleY = height / (float)bbox.Height;
            float scale = Math.Min(scaleX, scaleY) * 0.9f;
            float offsetX = (width - (float)bbox.Width * scale) / 2f;
            float offsetY = (height - (float)bbox.Height * scale) / 2f;

            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = strokeWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            var context = new RenderContext(canvas, bbox, scale, offsetX, offsetY, height, paint);

            foreach (var entity in entities)
            {
                if (entity == null)
                {
                    continue;
                }

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
            if (_rendererMap.TryGetValue(entity.GetType(), out var renderer))
            {
                return renderer;
            }

            return _rendererFallback.FirstOrDefault(r => r.EntityType.IsAssignableFrom(entity.GetType()));
        }
    }
}
