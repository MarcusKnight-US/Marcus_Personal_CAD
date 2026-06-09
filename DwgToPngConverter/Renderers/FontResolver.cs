using System;
using System.Collections.Generic;
using SkiaSharp;

namespace DwgToPngConverter.Renderers
{
    public static class FontResolver
    {
        private static readonly Dictionary<string, SKTypeface> _typefaceCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new();

        /// <summary>
        /// Gets or sets the default font family used for all text rendering.
        /// </summary>
        public static string DefaultFontFamily { get; set; } = "Arial";

        /// <summary>
        /// Resolves the default typeface. Font support based on individual drawing objects/styles
        /// has been removed; all text now renders using the DefaultFontFamily.
        /// </summary>
        public static SKTypeface ResolveTypeface()
        {
            string fontFamily = DefaultFontFamily;
            lock (_lock)
            {
                if (_typefaceCache.TryGetValue(fontFamily, out var cachedTypeface))
                {
                    return cachedTypeface;
                }

                var tf = SKTypeface.FromFamilyName(fontFamily);
                if (tf != null)
                {
                    _typefaceCache[fontFamily] = tf;
                    return tf;
                }

                // Fallback to Arial if DefaultFontFamily cannot be resolved
                var fallbackTf = SKTypeface.FromFamilyName("Arial");
                _typefaceCache[fontFamily] = fallbackTf;
                return fallbackTf;
            }
        }
    }
}

