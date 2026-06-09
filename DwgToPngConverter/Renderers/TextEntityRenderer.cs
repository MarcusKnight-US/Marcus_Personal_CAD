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

            float screenHeight = (float)(text.Height * context.EffectiveScale * context.TextMultiplier);

            double angle = text.Rotation % (2 * Math.PI);
            if (angle < 0) angle += 2 * Math.PI;

            bool flipped = false;
            if (angle > Math.PI / 2.001 && angle <= 3.0 * Math.PI / 1.999)
            {
                angle -= Math.PI;
                flipped = true;
            }

            context.Canvas.Save();
            var screenPos = context.ToScreenPoint(text.InsertPoint);
            context.Canvas.Translate(screenPos.X, screenPos.Y);
            context.Canvas.RotateRadians((float)-angle);

            var typeface = FontResolver.ResolveTypeface();
            using var font = new SKFont(typeface, screenHeight);
            using var paint = new SKPaint
            {
                Color = context.Paint.Color,
                IsAntialias = true
            };

            string cleaned = MTextHelper.CleanMText(text.Value);
            var lines = cleaned.Replace("\r", "").Split('\n');
            float lineSpacing = screenHeight * 1.3f;
            float yOffset = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                float xOffset = 0;
                if (flipped)
                {
                    xOffset = -font.MeasureText(lines[i]);
                }

                context.Canvas.DrawText(lines[i], xOffset, yOffset, font, paint);
                yOffset += lineSpacing;
            }

            context.Canvas.Restore();
        }
    }
}
