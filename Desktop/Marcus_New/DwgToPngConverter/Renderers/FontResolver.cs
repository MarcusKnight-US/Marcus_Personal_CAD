using System;
using System.IO;
using System.Collections.Generic;
using SkiaSharp;

namespace DwgToPngConverter.Renderers
{
    public static class FontResolver
    {
        private static readonly Dictionary<string, SKTypeface> _typefaceCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new();

        public static SKTypeface ResolveTypeface(string? fontFilename)
        {
            if (string.IsNullOrWhiteSpace(fontFilename))
            {
                return GetCachedTypeface("Arial", () => SKTypeface.FromFamilyName("Arial"));
            }

            lock (_lock)
            {
                if (_typefaceCache.TryGetValue(fontFilename, out var cachedTypeface))
                {
                    return cachedTypeface;
                }

                // 1. Try to load from system font files if there is a filename extension
                string fontFilenameLower = fontFilename.Trim();
                string[] searchDirectories = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Windows\\Fonts")
                };

                foreach (var dir in searchDirectories)
                {
                    if (string.IsNullOrEmpty(dir)) continue;

                    string fullPath = Path.Combine(dir, fontFilenameLower);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            var tf = SKTypeface.FromFile(fullPath);
                            if (tf != null)
                            {
                                Console.WriteLine($"[FontResolver] Loaded font file from system: {fullPath}");
                                _typefaceCache[fontFilename] = tf;
                                return tf;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[FontResolver] Warning: Failed to load font file '{fullPath}': {ex.Message}");
                        }
                    }
                }

                // 2. Try loading by family name (without extension)
                string familyName = Path.GetFileNameWithoutExtension(fontFilenameLower);
                
                // Handle common SHX files by removing double extensions or similar
                if (familyName.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
                {
                    familyName = Path.GetFileNameWithoutExtension(familyName);
                }

                var tfFamily = SKTypeface.FromFamilyName(familyName);
                // Verify that FromFamilyName returned a match, not the Segoe UI default fallback
                // (Unless the requested font was actually Segoe UI)
                if (tfFamily != null && 
                    (!tfFamily.FamilyName.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase) || 
                     familyName.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"[FontResolver] Resolved font family name: '{familyName}' -> '{tfFamily.FamilyName}'");
                    _typefaceCache[fontFilename] = tfFamily;
                    return tfFamily;
                }

                // 3. Fallback to Arial
                Console.WriteLine($"[FontResolver] Fallback to Arial for font: '{fontFilename}'");
                var fallbackTf = GetCachedTypeface("Arial", () => SKTypeface.FromFamilyName("Arial"));
                _typefaceCache[fontFilename] = fallbackTf;
                return fallbackTf;
            }
        }

        private static SKTypeface GetCachedTypeface(string key, Func<SKTypeface> creator)
        {
            lock (_lock)
            {
                if (_typefaceCache.TryGetValue(key, out var tf))
                {
                    return tf;
                }
                var newTf = creator();
                _typefaceCache[key] = newTf;
                return newTf;
            }
        }
    }
}
