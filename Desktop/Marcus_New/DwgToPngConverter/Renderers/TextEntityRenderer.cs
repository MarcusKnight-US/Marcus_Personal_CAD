using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class TextEntityRenderer : EntityRenderer<TextEntity>
    {
        protected override void Draw(RenderContext context, TextEntity text)
        {
            if (text == null || string.IsNullOrEmpty(text.Value)) return;

            float screenHeight = (float)(text.Height * context.Scale);

            context.Canvas.Save();
            var screenPos = context.ToScreenPoint(text.InsertPoint);
            context.Canvas.Translate(screenPos.X, screenPos.Y);
            context.Canvas.RotateRadians((float)-text.Rotation);

            var typeface = FontResolver.ResolveTypeface(text.Style?.Filename);
            using var font = new SKFont(typeface, screenHeight);
            using var paint = new SKPaint
            {
                Color = context.Paint.Color,
                IsAntialias = true
            };

            context.Canvas.DrawText(text.Value, 0, 0, font, paint);
            context.Canvas.Restore();
        }
    }
}
