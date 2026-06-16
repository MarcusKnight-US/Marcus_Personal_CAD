using CSMath;
using System;

namespace DwgToPngConverter.Geometry
{
    public readonly struct Transformation
    {
        public double M11 { get; }
        public double M12 { get; }
        public double M21 { get; }
        public double M22 { get; }
        public double M31 { get; }
        public double M32 { get; }
        public double ScaleZ { get; }
        public double TranslateZ { get; }

        public static readonly Transformation Identity = new Transformation(1, 0, 0, 1, 0, 0, 1, 0);

        public double ScaleX => Math.Sqrt(M11 * M11 + M12 * M12);
        public double ScaleY => Math.Sqrt(M21 * M21 + M22 * M22);
        public double Rotation => Math.Atan2(M12, M11);

        public Transformation(
            double m11, double m12,
            double m21, double m22,
            double m31, double m32,
            double scaleZ = 1.0, double translateZ = 0.0)
        {
            M11 = m11; M12 = m12;
            M21 = m21; M22 = m22;
            M31 = m31; M32 = m32;
            ScaleZ = scaleZ;
            TranslateZ = translateZ;
        }

        public Transformation(
            double scaleX, double scaleY, double scaleZ,
            double rotation,
            XYZ insertPoint,
            double colSpacing = 0.0, double rowSpacing = 0.0,
            int colIndex = 0, int rowIndex = 0)
        {
            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);
            double offsetX = colIndex * colSpacing;
            double offsetY = rowIndex * rowSpacing;

            M11 = scaleX * cos;
            M12 = scaleX * sin;
            M21 = -scaleY * sin;
            M22 = scaleY * cos;
            M31 = offsetX * cos - offsetY * sin + insertPoint.X;
            M32 = offsetX * sin + offsetY * cos + insertPoint.Y;
            ScaleZ = scaleZ;
            TranslateZ = insertPoint.Z;
        }

        public XYZ TransformPoint(XYZ point)
        {
            double x = point.X * M11 + point.Y * M21 + M31;
            double y = point.X * M12 + point.Y * M22 + M32;
            double z = point.Z * ScaleZ + TranslateZ;
            return new XYZ(x, y, z);
        }

        public XY TransformPoint(XY point)
        {
            double x = point.X * M11 + point.Y * M21 + M31;
            double y = point.X * M12 + point.Y * M22 + M32;
            return new XY(x, y);
        }

        public XYZ TransformVector(XYZ vector)
        {
            double x = vector.X * M11 + vector.Y * M21;
            double y = vector.X * M12 + vector.Y * M22;
            double z = vector.Z * ScaleZ;
            return new XYZ(x, y, z);
        }

        public XY TransformVector(XY vector)
        {
            double x = vector.X * M11 + vector.Y * M21;
            double y = vector.X * M12 + vector.Y * M22;
            return new XY(x, y);
        }

        public Transformation Combine(Transformation other)
        {
            return new Transformation(
                M11 * other.M11 + M12 * other.M21,
                M11 * other.M12 + M12 * other.M22,
                M21 * other.M11 + M22 * other.M21,
                M21 * other.M12 + M22 * other.M22,
                M31 * other.M11 + M32 * other.M21 + other.M31,
                M31 * other.M12 + M32 * other.M22 + other.M32,
                ScaleZ * other.ScaleZ,
                TranslateZ * other.ScaleZ + other.TranslateZ
            );
        }
    }
}
