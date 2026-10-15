using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace EMISAPIS.Helpers
{
    /// <summary>
    /// Security helper for validating uploaded files: size limits, allowed extensions,
    /// MIME types, magic bytes headers, path traversal, and malicious script checks.
    /// </summary>
    public static class FileValidationHelper
    {
        public const long DefaultMaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        public const long MaxPdfSizeBytes = 5 * 1024 * 1024;          // 5 MB
        public const long MaxImageSizeBytes = 5 * 1024 * 1024;        // 5 MB

        public static readonly string[] AllowedPdfExtensions = { ".pdf" };
        public static readonly string[] AllowedDocExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };
        public static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png" };

        private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-
        private static readonly byte[] JpegMagicBytes = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47 };

        /// <summary>
        /// Validates file existence, non-emptiness, extension, size limit, and magic bytes.
        /// </summary>
        public static bool ValidateFile(
            IFormFile? file,
            out string errorMessage,
            long maxSizeBytes = DefaultMaxFileSizeBytes,
            string[]? allowedExtensions = null)
        {
            errorMessage = string.Empty;

            if (file == null || file.Length == 0)
            {
                errorMessage = "Please select a file to upload.";
                return false;
            }

            if (file.Length > maxSizeBytes)
            {
                var mb = maxSizeBytes / (1024 * 1024);
                errorMessage = $"File size exceeds maximum allowed limit of {mb} MB.";
                return false;
            }

            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension))
            {
                errorMessage = "Uploaded file has no extension.";
                return false;
            }

            var allowed = allowedExtensions ?? AllowedDocExtensions;
            if (!allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                errorMessage = $"Invalid file type '{extension}'. Allowed types: {string.Join(", ", allowed)}.";
                return false;
            }

            // Path traversal prevention in file name
            var rawFileName = file.FileName;
            if (rawFileName.Contains("..") || rawFileName.Contains('/') || rawFileName.Contains('\\'))
            {
                errorMessage = "Invalid file name containing path traversal characters.";
                return false;
            }

            // Deep inspect header magic bytes
            try
            {
                using var stream = file.OpenReadStream();
                var headerBuffer = new byte[Math.Min(1024, (int)file.Length)];
                var read = stream.Read(headerBuffer, 0, headerBuffer.Length);
                if (read < 4)
                {
                    errorMessage = "File content is corrupted or empty.";
                    return false;
                }

                if (!VerifyMagicBytes(headerBuffer, extension))
                {
                    errorMessage = $"Corrupted file content or mismatched file extension '{extension}'.";
                    return false;
                }

                // Check for embedded malicious scripts
                if (HasDangerousContent(headerBuffer))
                {
                    errorMessage = "File contains potentially malicious embedded scripts or actions.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Unable to read file for validation: {ex.Message}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that the file is a valid PDF within the size limit.
        /// </summary>
        public static bool ValidatePdf(IFormFile? file, out string errorMessage, long maxSizeBytes = MaxPdfSizeBytes)
        {
            return ValidateFile(file, out errorMessage, maxSizeBytes, AllowedPdfExtensions);
        }

        /// <summary>
        /// Validates that the file is a PDF or Image (JPG, JPEG, PNG) within the size limit.
        /// </summary>
        public static bool ValidateDocumentOrImage(IFormFile? file, out string errorMessage, long maxSizeBytes = DefaultMaxFileSizeBytes)
        {
            return ValidateFile(file, out errorMessage, maxSizeBytes, AllowedDocExtensions);
        }

        /// <summary>
        /// Verifies magic byte headers against extension.
        /// </summary>
        private static bool VerifyMagicBytes(byte[] header, string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".pdf":
                    return header.Length >= 5 &&
                           header[0] == PdfMagicBytes[0] &&
                           header[1] == PdfMagicBytes[1] &&
                           header[2] == PdfMagicBytes[2] &&
                           header[3] == PdfMagicBytes[3] &&
                           header[4] == PdfMagicBytes[4];

                case ".jpg":
                case ".jpeg":
                    return header.Length >= 3 &&
                           header[0] == JpegMagicBytes[0] &&
                           header[1] == JpegMagicBytes[1] &&
                           header[2] == JpegMagicBytes[2];

                case ".png":
                    return header.Length >= 4 &&
                           header[0] == PngMagicBytes[0] &&
                           header[1] == PngMagicBytes[1] &&
                           header[2] == PngMagicBytes[2] &&
                           header[3] == PngMagicBytes[3];

                default:
                    return false;
            }
        }

        /// <summary>
        /// Checks header bytes for dangerous scripting content (e.g. /JavaScript, &lt;script).
        /// </summary>
        public static bool HasDangerousContent(byte[] bytes)
        {
            try
            {
                var content = Encoding.ASCII.GetString(bytes);
                string[] dangerousPatterns =
                {
                    "/JavaScript",
                    "/JS",
                    "/Launch",
                    "<script",
                    "javascript:",
                    "<?php",
                    "eval(",
                    "cmd.exe",
                    "/bin/sh"
                };

                foreach (var pattern in dangerousPatterns)
                {
                    if (content.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore decoding errors
            }

            return false;
        }

        /// <summary>
        /// Sanitizes file name by stripping directory paths and illegal file name characters.
        /// </summary>
        public static string SanitizeFileName(string fileName, string fallbackPrefix = "upload")
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return $"{fallbackPrefix}_{Guid.NewGuid():N}";

            var baseName = Path.GetFileName(fileName);
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleanChars = baseName.Where(c => !invalidChars.Contains(c) && c != '\'' && c != '\"' && c != ';' && c != ':').ToArray();
            var cleanName = new string(cleanChars);

            if (string.IsNullOrWhiteSpace(cleanName))
                return $"{fallbackPrefix}_{Guid.NewGuid():N}";

            return cleanName;
        }
    }
}
