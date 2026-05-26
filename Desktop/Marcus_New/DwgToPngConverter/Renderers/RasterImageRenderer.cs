using System;
using System.IO;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class RasterImageRenderer : EntityRenderer<RasterImage>
    {
        protected override void Draw(RenderContext context, RasterImage rasterImage)
        {
            if (rasterImage.Definition == null)
            {
                return;
            }

            string? resolvedPath = null;
            if (!string.IsNullOrEmpty(rasterImage.Definition.FileName))
            {
                if (File.Exists(rasterImage.Definition.FileName))
                {
                    resolvedPath = rasterImage.Definition.FileName;
                }
                else
                {
                    string fileName = Path.GetFileName(rasterImage.Definition.FileName);
                    if (!string.IsNullOrEmpty(context.DwgFilePath))
                    {
                        string? dwgDir = Path.GetDirectoryName(context.DwgFilePath);
                        if (!string.IsNullOrEmpty(dwgDir))
                        {
                            string candidate = Path.Combine(dwgDir, fileName);
                            if (File.Exists(candidate))
                            {
                                resolvedPath = candidate;
                            }
                        }
                    }

                    if (resolvedPath == null)
                    {
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

            try
            {
                using var bitmap = SKBitmap.Decode(resolvedPath);
                if (bitmap == null)
                {
                    Console.WriteLine($"Warning: Failed to decode image from '{resolvedPath}'");
                    return;
                }

                int w = bitmap.Width;
                int h = bitmap.Height;

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

                using var image = SKImage.FromBitmap(bitmap);
                if (image != null)
                {
                    context.Canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
                }

                context.Canvas.Restore();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering raster image from '{resolvedPath}': {ex.Message}");
            }
        }
    }
}
