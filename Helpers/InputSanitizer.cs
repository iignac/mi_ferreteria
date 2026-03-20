using System.Text.RegularExpressions;

namespace mi_ferreteria.Helpers
{
    public static class InputSanitizer
    {
        private static readonly Regex MultiWhitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public static string? NormalizeName(string? value)
        {
            if (value == null) return null;
            var trimmed = value.Trim();
            if (trimmed.Length == 0) return string.Empty;
            return MultiWhitespace.Replace(trimmed, " ");
        }

        public static string? NormalizeEmail(string? value)
        {
            if (value == null) return null;
            var trimmed = value.Trim();
            if (trimmed.Length == 0) return string.Empty;
            return trimmed.ToLowerInvariant();
        }
    }
}
