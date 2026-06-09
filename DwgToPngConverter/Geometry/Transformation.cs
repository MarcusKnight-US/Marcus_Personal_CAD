using CSMath;
using System;

namespace DwgToPngConverter.Geometry
{
    public readonly struct Transformation
    {
        public double ScaleX { get; }
        public double ScaleY { get; }
        public double ScaleZ { get; }
        public double Rotation { get; }
        public XYZ InsertPoint { get; }
        public double OffsetX { get; }
        public double OffsetY { get; }
        public double Cos { get; }
        public double Sin { get; }

        public Transformation(
            double scaleX, double scaleY, double scaleZ,
            double rotation,
            XYZ insertPoint,
            double colSpacing = 0.0, double rowSpacing = 0.0,
            int colIndex = 0, int rowIndex = 0)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;
            Rotation = rotation;
            InsertPoint = insertPoint;
            OffsetX = colIndex * colSpacing;
            OffsetY = rowIndex * rowSpacing;
            Cos = Math.Cos(rotation);
            Sin = Math.Sin(rotation);
        }

        public XYZ TransformPoint(XYZ point)
        {
            double x = point.X * ScaleX + OffsetX;
            double y = point.Y * ScaleY + OffsetY;
            double z = point.Z * ScaleZ;

            double rx = x * Cos - y * Sin;
            double ry = x * Sin + y * Cos;
            double rz = z;

            return new XYZ(rx + InsertPoint.X, ry + InsertPoint.Y, rz + InsertPoint.Z);
        }

        public XY TransformPoint(XY point)
        {
            double x = point.X * ScaleX + OffsetX;
            double y = point.Y * ScaleY + OffsetY;

            double rx = x * Cos - y * Sin;
            double ry = x * Sin + y * Cos;

            return new XY(rx + InsertPoint.X, ry + InsertPoint.Y);
        }

        public XYZ TransformVector(XYZ vector)
        {
            double x = vector.X * ScaleX;
            double y = vector.Y * ScaleY;
            double z = vector.Z * ScaleZ;

            double rx = x * Cos - y * Sin;
            double ry = x * Sin + y * Cos;
            double rz = z;

            return new XYZ(rx, ry, rz);
        }
    }
}
