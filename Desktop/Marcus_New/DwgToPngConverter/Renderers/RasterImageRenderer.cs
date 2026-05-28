using System;
using System.IO;
using System.Collections.Concurrent;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class RasterImageRenderer : EntityRenderer<RasterImage>
    {
        private class CachedImage
        {
            public SKImage Image { get; }
            public int Width { get; }
            public int Height { get; }

            public CachedImage(SKImage image, int w, int h)
            {
                Image = image;
                Width = w;
                Height = h;
            }
        }

        private static readonly ConcurrentDictionary<string, CachedImage> _imageCache = new();

        protected override void Draw(RenderContext context, RasterImage rasterImage)
        {
            if (rasterImage.Definition == null)
            {
                return;
            }

            string? resolvedPath = null;
            if (!string.IsNullOrEmpty(rasterImage.Definition.FileName))
            {
                // 1. Try absolute or direct relative path as is
                if (File.Exists(rasterImage.Definition.FileName))
                {
                    resolvedPath = rasterImage.Definition.FileName;
                }
                else
                {
                    // 2. Try resolving the entire relative path relative to the DWG file directory
                    if (!string.IsNullOrEmpty(context.DwgFilePath))
                    {
                        string? dwgDir = Path.GetDirectoryName(context.DwgFilePath);
                        if (!string.IsNullOrEmpty(dwgDir))
                        {
                            try
                            {
                                string candidateRelative = Path.GetFullPath(Path.Combine(dwgDir, rasterImage.Definition.FileName));
                                if (File.Exists(candidateRelative))
                                {
                                    resolvedPath = candidateRelative;
                                }
                            }
                            catch {}
                        }
                    }

                    // 3. Fallback: try resolving just the filename in the same directory as the DWG file
                    if (resolvedPath == null && !string.IsNullOrEmpty(context.DwgFilePath))
                    {
                        string? dwgDir = Path.GetDirectoryName(context.DwgFilePath);
                        if (!string.IsNullOrEmpty(dwgDir))
                        {
                            string fileName = Path.GetFileName(rasterImage.Definition.FileName);
                            string candidateNameOnly = Path.Combine(dwgDir, fileName);
                            if (File.Exists(candidateNameOnly))
                            {
                                resolvedPath = candidateNameOnly;
                            }
                        }
                    }

                    // 4. Fallback: try resolving just the filename in the current working directory
                    if (resolvedPath == null)
                    {
                        string fileName = Path.GetFileName(rasterImage.Definition.FileName);
                        if (File.Exists(fileName))
                        {
                            resolvedPath = fileName;
                        }
                    }
                }
            }

            if (resolvedPath == null)
            {
                Console.WriteLine($"Warning: Could not resolve image path for '{rasterImage.Definition.FileName}'");
                return;
            }

            // Look up or populate decoded image from static cache to eliminate repeated disk reads & decodes
            if (!_imageCache.TryGetValue(resolvedPath, out var cached))
            {
                try
                {
                    using var bitmap = SKBitmap.Decode(resolvedPath);
                    if (bitmap == null)
                    {
                        Console.WriteLine($"Warning: Failed to decode image from '{resolvedPath}'");
                        return;
                    }

                    var image = SKImage.FromBitmap(bitmap);
                    if (image == null)
                    {
                        Console.WriteLine($"Warning: Failed to create SKImage from bitmap for '{resolvedPath}'");
                        return;
                    }

                    cached = new CachedImage(image, bitmap.Width, bitmap.Height);
                    _imageCache[resolvedPath] = cached;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error decoding raster image from '{resolvedPath}': {ex.Message}");
                    return;
                }
            }

            try
            {
                int w = cached.Width;
                int h = cached.Height;

                // Affine transformation coefficients from local image space (lx, ly) to screen space (sx, sy)
                // sx = A * lx + B * ly + C
                // sy = D * lx + E * ly + F
                // Where:
                // A = scale * UVector.X
                // B = -scale * VVector.X
                // C = scale * (InsertionPoint.X + h * VVector.X) + offsetX - minX * scale
                // D = -scale * UVector.Y
                // E = scale * VVector.Y
                // F = -scale * (InsertionPoint.Y + h * VVector.Y) + height - offsetY + minY * scale

                float scale = context.Scale;
                float offsetX = context.OffsetX;
                float offsetY = context.OffsetY;
                double minX = context.BoundingBox.MinX;
                double minY = context.BoundingBox.MinY;
                int canvasHeight = context.Height;

                float a = (float)(scale * rasterImage.UVector.X);
                float b = (float)(-scale * rasterImage.VVector.X);
                float c = (float)(scale * (rasterImage.InsertPoint.X + h * rasterImage.VVector.X) + offsetX - minX * scale);
                float d = (float)(-scale * rasterImage.UVector.Y);
                float e = (float)(scale * rasterImage.VVector.Y);
                float f = (float)(-scale * (rasterImage.InsertPoint.Y + h * rasterImage.VVector.Y) + canvasHeight - offsetY + minY * scale);

                var matrix = new SKMatrix(a, b, c, d, e, f, 0f, 0f, 1f);

                context.Canvas.Save();
                context.Canvas.Concat(matrix);

                using var paint = new SKPaint
                {
                    IsAntialias = true
                };

                context.Canvas.DrawImage(cached.Image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);

                context.Canvas.Restore();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering cached raster image from '{resolvedPath}': {ex.Message}");
            }
        }
    }
}
