using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public class SolidRenderer : EntityRenderer<Solid>
    {
        protected override void Draw(RenderContext context, Solid solid)
        {
            if (solid == null) return;

            var p1 = context.ToScreenPoint(solid.FirstCorner);
            var p2 = context.ToScreenPoint(solid.SecondCorner);
            var p3 = context.ToScreenPoint(solid.ThirdCorner);
            var p4 = context.ToScreenPoint(solid.FourthCorner);

            using var path = new SKPath();
            path.MoveTo(p1.X, p1.Y);
            path.LineTo(p2.X, p2.Y);
            // In AutoCAD SOLID, vertex ordering is:
            // FirstCorner, SecondCorner, FourthCorner, ThirdCorner
            // to avoid bow-tie self-intersection.
            path.LineTo(p4.X, p4.Y);
            path.LineTo(p3.X, p3.Y);
            path.Close();

            var paint = context.ResourceCache.GetPaint(context.Paint.Color, SKPaintStyle.Fill, isAntialias: true);

            context.Canvas.DrawPath(path, paint);
        }
    }
}
