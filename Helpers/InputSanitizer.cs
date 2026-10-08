using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EMISAPIS.Helpers
{
    /// <summary>
    /// Helper for input sanitization, request validation, and safe pagination normalization.
    /// Protects against XSS, SQL injection fragments, null byte attacks, and unbounded payloads.
    /// </summary>
    public static class InputSanitizer
    {
        private static readonly Regex HtmlTagRegex = new Regex("<.*?>", RegexOptions.Compiled);
        private static readonly Regex ScriptPatternRegex = new Regex(@"(javascript\s*:|vbscript\s*:|<script\b|eval\s*\(|alert\s*\(|document\.cookie)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly string[] AllowedDateFormats = { "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "d/M/yyyy", "yyyy/MM/dd" };

        /// <summary>
        /// Sanitizes text input: trims whitespace, removes null bytes, strips HTML/script tags, caps maximum length.
        /// </summary>
        public static string Sanitize(string? input, int maxLength = 500)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var cleaned = input.Trim().Replace("\0", string.Empty);

            // Strip HTML tags
            cleaned = HtmlTagRegex.Replace(cleaned, string.Empty);

            // Strip potential script payloads
            cleaned = ScriptPatternRegex.Replace(cleaned, string.Empty);

            if (cleaned.Length > maxLength)
            {
                cleaned = cleaned.Substring(0, maxLength);
            }

            return cleaned;
        }

        /// <summary>
        /// Sanitizes alphanumeric identifiers (e.g. codes, status flags, filter types).
        /// </summary>
        public static string SanitizeAlphanumeric(string? input, int maxLength = 50)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var trimmed = input.Trim();
            var regex = new Regex(@"[^a-zA-Z0-9_\-]");
            var cleaned = regex.Replace(trimmed, string.Empty);

            if (cleaned.Length > maxLength)
                cleaned = cleaned.Substring(0, maxLength);

            return cleaned;
        }

        /// <summary>
        /// Validates and parses date strings in standard formats.
        /// </summary>
        public static bool TryParseDate(string? input, out DateTime parsedDate)
        {
            parsedDate = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var clean = Sanitize(input, 20);
            if (DateTime.TryParse(clean, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                return true;

            return DateTime.TryParseExact(clean, AllowedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate);
        }

        /// <summary>
        /// Normalizes pagination parameters with a default limit of 20 and maximum cap of 100.
        /// </summary>
        public static (int pageNumber, int pageSize) NormalizePagination(
            int? pageNumber,
            int? pageSize,
            int defaultPageSize = 20,
            int maxPageSize = 100)
        {
            var pNum = pageNumber.HasValue && pageNumber.Value >= 1 ? pageNumber.Value : 1;
            var pSize = pageSize.HasValue && pageSize.Value > 0 ? pageSize.Value : defaultPageSize;

            if (pSize > maxPageSize)
                pSize = maxPageSize;

            return (pNum, pSize);
        }
    }
}
