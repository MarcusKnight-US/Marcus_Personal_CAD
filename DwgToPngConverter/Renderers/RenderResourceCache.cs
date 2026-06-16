using System;
using System.Collections.Generic;
using SkiaSharp;

namespace DwgToPngConverter.Renderers
{
    public class RenderResourceCache : IDisposable
    {
        private readonly Dictionary<FontKey, SKFont> _fontCache = new();
        private readonly Dictionary<PaintKey, SKPaint> _paintCache = new();

        public SKFont GetFont(SKTypeface typeface, float size, float scaleX = 1f)
        {
            var key = new FontKey(typeface, size, scaleX);
            if (!_fontCache.TryGetValue(key, out var font))
            {
                font = new SKFont(typeface, size);
                if (scaleX != 1f)
                {
                    font.ScaleX = scaleX;
                }
                _fontCache[key] = font;
            }
            return font;
        }

        public SKPaint GetPaint(SKColor color, SKPaintStyle style, bool isAntialias = true, float strokeWidth = -1f)
        {
            var key = new PaintKey(color, style, isAntialias, strokeWidth);
            if (!_paintCache.TryGetValue(key, out var paint))
            {
                paint = new SKPaint
                {
                    Color = color,
                    Style = style,
                    IsAntialias = isAntialias
                };
                if (strokeWidth >= 0)
                {
                    paint.StrokeWidth = strokeWidth;
                }
                _paintCache[key] = paint;
            }
            return paint;
        }

        public void Dispose()
        {
            foreach (var font in _fontCache.Values)
            {
                font.Dispose();
            }
            _fontCache.Clear();

            foreach (var paint in _paintCache.Values)
            {
                paint.Dispose();
            }
            _paintCache.Clear();
        }

        private readonly struct FontKey : IEquatable<FontKey>
        {
            public SKTypeface Typeface { get; }
            public float Size { get; }
            public float ScaleX { get; }

            public FontKey(SKTypeface typeface, float size, float scaleX)
            {
                Typeface = typeface;
                Size = size;
                ScaleX = scaleX;
            }

            public bool Equals(FontKey other)
            {
                return Typeface == other.Typeface &&
                       Size.Equals(other.Size) &&
                       ScaleX.Equals(other.ScaleX);
            }

            public override bool Equals(object? obj) => obj is FontKey other && Equals(other);

            public override int GetHashCode()
            {
                return HashCode.Combine(Typeface, Size, ScaleX);
            }
        }

        private readonly struct PaintKey : IEquatable<PaintKey>
        {
            public SKColor Color { get; }
            public SKPaintStyle Style { get; }
            public bool IsAntialias { get; }
            public float StrokeWidth { get; }

            public PaintKey(SKColor color, SKPaintStyle style, bool isAntialias, float strokeWidth)
            {
                Color = color;
                Style = style;
                IsAntialias = isAntialias;
                StrokeWidth = strokeWidth;
            }

            public bool Equals(PaintKey other)
            {
                return Color.Equals(other.Color) &&
                       Style == other.Style &&
                       IsAntialias == other.IsAntialias &&
                       StrokeWidth.Equals(other.StrokeWidth);
            }

            public override bool Equals(object? obj) => obj is PaintKey other && Equals(other);

            public override int GetHashCode()
            {
                return HashCode.Combine(Color, Style, IsAntialias, StrokeWidth);
            }
        }
    }
}
