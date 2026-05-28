using System;
using System.Text.RegularExpressions;

namespace DwgToPngConverter.Geometry
{
    public static class MTextHelper
    {
        private static readonly Regex _regexFormat1 = new(@"\\[A-Za-z0-9\.]+;", RegexOptions.Compiled);
        private static readonly Regex _regexFormat2 = new(@"\\[Ff][^;]*;", RegexOptions.Compiled);
        private static readonly Regex _regexFormat3 = new(@"\\[Cc][^;]*;", RegexOptions.Compiled);
        private static readonly Regex _regexFormat4 = new(@"\\[Hh][^;]*;", RegexOptions.Compiled);
        private static readonly Regex _regexFormat5 = new(@"\\[Ww][^;]*;", RegexOptions.Compiled);
        private static readonly Regex _regexFormat6 = new(@"\\[Tt][^;]*;", RegexOptions.Compiled);
        private static readonly Regex _regexFormat7 = new(@"\\[Qq][^;]*;", RegexOptions.Compiled);
        private static readonly Regex _regexFormat8 = new(@"\\[SsloOL][^;]*;", RegexOptions.Compiled);
        private static readonly Regex _regexFormat9 = new(@"\\[A-Za-z][^;]*;", RegexOptions.Compiled);
        private static readonly Regex _regexStack = new(@"\\[Ss]([^;]+);", RegexOptions.Compiled);

        public static string CleanMText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Handle stacks first to preserve the fraction numbers (e.g. \S1/2; or \S1^2;)
            string cleaned = _regexStack.Replace(text, match =>
            {
                string inner = match.Groups[1].Value;
                return inner.Replace("^", "/").Replace("#", "/");
            });

            cleaned = cleaned.Replace("\\P", "\n").Replace("\\p", "\n");
            
            // Strip inline formatting tags that don't have terminating semicolons
            cleaned = cleaned.Replace("\\L", "").Replace("\\l", "")
                             .Replace("\\O", "").Replace("\\o", "")
                             .Replace("\\K", "").Replace("\\k", "")
                             .Replace("\\~", " ");

            // Standard diameter and annotation symbol mappings
            cleaned = cleaned.Replace("%%c", "Ø").Replace("%%C", "Ø");
            cleaned = cleaned.Replace("%%d", "°").Replace("%%D", "°");
            cleaned = cleaned.Replace("%%p", "±").Replace("%%P", "±");
            cleaned = cleaned.Replace("∅", "Ø");
            cleaned = cleaned.Replace("\u2205", "Ø");
            cleaned = cleaned.Replace("\u2300", "Ø");

            // Strip other AutoCAD formatting overrides using precompiled Regexes
            cleaned = _regexFormat1.Replace(cleaned, "");
            cleaned = _regexFormat2.Replace(cleaned, "");
            cleaned = _regexFormat3.Replace(cleaned, "");
            cleaned = _regexFormat4.Replace(cleaned, "");
            cleaned = _regexFormat5.Replace(cleaned, "");
            cleaned = _regexFormat6.Replace(cleaned, "");
            cleaned = _regexFormat7.Replace(cleaned, "");
            cleaned = _regexFormat8.Replace(cleaned, "");
            cleaned = _regexFormat9.Replace(cleaned, "");

            cleaned = cleaned.Replace("{", "").Replace("}", "");
            cleaned = cleaned.Replace("\\\\", "\\");

            return cleaned;
        }
    }
}
