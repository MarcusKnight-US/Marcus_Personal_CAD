using System;
using SkiaSharp;
using ACadSharp.Entities;
using DwgToPngConverter.Geometry;

namespace DwgToPngConverter.Renderers
{
    public abstract class EntityRenderer<T> : IEntityRenderer where T : Entity
    {
        public Type EntityType => typeof(T);

        public bool CanRender(Entity entity)
        {
            return entity is T;
        }

        public void Draw(RenderContext context, Entity entity)
        {
            if (entity is T typedEntity)
            {
                Draw(context, typedEntity);
            }
        }

        protected abstract void Draw(RenderContext context, T entity);
    }
}
