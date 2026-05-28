using System;
using SkiaSharp;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
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
            float screenHeight = (float)(mtext.Height * context.EffectiveScale);

            double angle = mtext.Rotation % (2 * Math.PI);
            if (angle < 0) angle += 2 * Math.PI;

            bool flipped = false;
            if (angle > Math.PI / 2.001 && angle <= 3.0 * Math.PI / 1.999)
            {
                angle -= Math.PI;
                flipped = true;
            }

            context.Canvas.Save();
            var screenPos = context.ToScreenPoint(mtext.InsertPoint);
            context.Canvas.Translate(screenPos.X, screenPos.Y);
            context.Canvas.RotateRadians((float)-angle);

            var typeface = FontResolver.ResolveTypeface();
            using var font = new SKFont(typeface, screenHeight);
            if (mtext.HorizontalWidth > 0 && mtext.HorizontalWidth <= 2.0)
            {
                font.ScaleX = (float)mtext.HorizontalWidth;
            }
            using var paint = new SKPaint
            {
                Color = context.Paint.Color,
                IsAntialias = true
            };

            // Dynamic word-wrapping based on RectangleWidth
            float wrapWidth = (float)(mtext.RectangleWidth * context.EffectiveScale);
            string[] lines;
            if (wrapWidth > 0)
            {
                lines = WrapText(cleaned, font, wrapWidth).ToArray();
            }
            else
            {
                lines = cleaned.Split('\n');
            }

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
            if (flipped)
            {
                int[] flipMap = { 0, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
                if (attachmentVal >= 1 && attachmentVal <= 9)
                {
                    attachmentVal = flipMap[attachmentVal];
                }
            }

            // Check if MText is part of a dimension block and has a collinear leader landing line
            if (mtext.Owner is BlockRecord br && br.Name.StartsWith("*D"))
            {
                float textY = (float)mtext.InsertPoint.Y;
                float textX = (float)mtext.InsertPoint.X;

                double tol = 0.1;
                System.Collections.Generic.List<double> collinearXEnds = new();
                foreach (var entity in br.Entities)
                {
                    if (entity is Line line)
                    {
                        if (Math.Abs(line.StartPoint.Y - textY) < tol)
                        {
                            collinearXEnds.Add(line.StartPoint.X);
                        }
                        if (Math.Abs(line.EndPoint.Y - textY) < tol)
                        {
                            collinearXEnds.Add(line.EndPoint.X);
                        }
                    }
                }

                if (collinearXEnds.Count > 0)
                {
                    collinearXEnds.Sort();

                    bool hasLeft = false;
                    bool hasRight = false;
                    double closestLeft = double.MinValue;
                    double closestRight = double.MaxValue;

                    foreach (double x in collinearXEnds)
                    {
                        if (x < textX - tol)
                        {
                            hasLeft = true;
                            if (x > closestLeft) closestLeft = x;
                        }
                        else if (x > textX + tol)
                        {
                            hasRight = true;
                            if (x < closestRight) closestRight = x;
                        }
                    }

                    if (hasLeft && !hasRight)
                    {
                        // Single-sided leader to the left (e.g. *D3)
                        // Align MiddleRight relative to textX, so the text block starts exactly at closestLeft and ends at textX
                        attachmentVal = 6; 
                    }
                    else if (hasRight && !hasLeft)
                    {
                        // Single-sided leader to the right
                        // Align MiddleLeft relative to textX, so the text block starts at textX and ends at closestRight
                        attachmentVal = 4;
                    }
                }
            }

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
                    startY = 0.8f * lineHeight;
                    break;
                case 4: // MiddleLeft
                case 5: // MiddleCenter
                case 6: // MiddleRight
                    startY = -blockHeight / 2f + 0.8f * lineHeight;
                    break;
                case 7: // BottomLeft
                case 8: // BottomCenter
                case 9: // BottomRight
                    startY = -blockHeight + 0.8f * lineHeight;
                    break;
                default:
                    startY = 0.8f * lineHeight;
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

        private static System.Collections.Generic.List<string> WrapText(string text, SKFont font, float maxWidth)
        {
            var result = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(text))
            {
                result.Add(string.Empty);
                return result;
            }

            var paragraphs = text.Replace("\r", "").Split('\n');
            foreach (var para in paragraphs)
            {
                if (string.IsNullOrEmpty(para))
                {
                    result.Add(string.Empty);
                    continue;
                }

                // If paragraph fits, no need to wrap
                if (font.MeasureText(para) <= maxWidth)
                {
                    result.Add(para);
                    continue;
                }

                var words = para.Split(' ');
                var currentLine = new System.Text.StringBuilder();

                foreach (var word in words)
                {
                    if (currentLine.Length == 0)
                    {
                        currentLine.Append(word);
                    }
                    else
                    {
                        string testLine = currentLine.ToString() + " " + word;
                        if (font.MeasureText(testLine) <= maxWidth)
                        {
                            currentLine.Append(" ").Append(word);
                        }
                        else
                        {
                            result.Add(currentLine.ToString());
                            currentLine.Clear();
                            currentLine.Append(word);
                        }
                    }
                }

                if (currentLine.Length > 0)
                {
                    result.Add(currentLine.ToString());
                }
            }

            return result;
        }
    }
}

