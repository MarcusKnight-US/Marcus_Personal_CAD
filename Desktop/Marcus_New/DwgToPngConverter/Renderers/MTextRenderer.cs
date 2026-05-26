using SkiaSharp;
using ACadSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;
using System.Text.RegularExpressions;

namespace DwgToPngConverter.Renderers
{
    public class MTextRenderer : EntityRenderer<MText>
    {
        protected override void Draw(RenderContext context, MText mtext)
        {
            if (mtext == null || string.IsNullOrEmpty(mtext.Value)) return;

            string cleaned = CleanMText(mtext.Value);
            var lines = cleaned.Split('\n');

            float screenHeight = (float)(mtext.Height * context.Scale);

            context.Canvas.Save();
            var screenPos = context.ToScreenPoint(mtext.InsertPoint);
            context.Canvas.Translate(screenPos.X, screenPos.Y);
            context.Canvas.RotateRadians((float)-mtext.Rotation);

            var typeface = FontResolver.ResolveTypeface(mtext.Style?.Filename);
            using var font = new SKFont(typeface, screenHeight);
            using var paint = new SKPaint
            {
                Color = context.Paint.Color,
                IsAntialias = true
            };

            // Calculate text block dimensions and individual line widths
            float[] lineWidths = new float[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                lineWidths[i] = font.MeasureText(lines[i]);
            }

            float lineHeight = screenHeight;
            float lineSpacing = lineHeight * 1.3f;
            float blockHeight = lines.Length > 0 ? (lines.Length * lineSpacing - lineHeight * 0.3f) : 0;

            int attachmentVal = (int)mtext.AttachmentPoint;

            // Determine vertical offset for the first line baseline based on AttachmentPoint
            // 1=TopLeft, 2=TopCenter, 3=TopRight
            // 4=MiddleLeft, 5=MiddleCenter, 6=MiddleRight
            // 7=BottomLeft, 8=BottomCenter, 9=BottomRight
            float startY = 0;
            switch (attachmentVal)
            {
                case 1: // TopLeft
                case 2: // TopCenter
                case 3: // TopRight
                    startY = lineHeight;
                    break;
                case 4: // MiddleLeft
                case 5: // MiddleCenter
                case 6: // MiddleRight
                    startY = -blockHeight / 2f + lineHeight;
                    break;
                case 7: // BottomLeft
                case 8: // BottomCenter
                case 9: // BottomRight
                    startY = -blockHeight + lineHeight;
                    break;
                default:
                    startY = lineHeight;
                    break;
            }

            // Draw lines with custom X offset for each line based on AttachmentPoint
            float yOffset = startY;
            for (int i = 0; i < lines.Length; i++)
            {
                float lineWidth = lineWidths[i];
                float xOffset = 0;

                switch (attachmentVal)
                {
                    case 1: // TopLeft
                    case 4: // MiddleLeft
                    case 7: // BottomLeft
                        xOffset = 0;
                        break;
                    case 2: // TopCenter
                    case 5: // MiddleCenter
                    case 8: // BottomCenter
                        xOffset = -lineWidth / 2f;
                        break;
                    case 3: // TopRight
                    case 6: // MiddleRight
                    case 9: // BottomRight
                        xOffset = -lineWidth;
                        break;
                    default:
                        xOffset = 0;
                        break;
                }

                context.Canvas.DrawText(lines[i], xOffset, yOffset, font, paint);
                yOffset += lineSpacing;
            }

            context.Canvas.Restore();
        }

        private static string CleanMText(string text)
        {
            return MTextHelper.CleanMText(text);
        }
    }
}
