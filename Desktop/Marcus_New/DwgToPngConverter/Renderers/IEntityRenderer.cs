using System;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public interface IEntityRenderer
    {
        Type EntityType { get; }
        bool CanRender(Entity entity);
        void Draw(RenderContext context, Entity entity);
    }
}
