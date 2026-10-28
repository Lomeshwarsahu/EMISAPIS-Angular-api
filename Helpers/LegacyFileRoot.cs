namespace EMISAPIS.Helpers
{
    /// <summary>
    /// Root folder that holds the legacy EMSRole upload folders (Upload_invoiceDoc, Upload_SDdeatil, Uploads, ...).
    /// Set "FileStorage:LegacyRoot" (e.g. "D:\\EMSAPI\\Documents", or "Documents" = relative to the API folder);
    /// defaults to the "EMSRole" folder next to the API.
    /// </summary>
    public static class LegacyFileRoot
    {
        public static string Resolve(IConfiguration configuration, IWebHostEnvironment env)
        {
            var configured = configuration["FileStorage:LegacyRoot"]?.Trim();
            if (string.IsNullOrWhiteSpace(configured))
                return Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "EMSRole"));

            // Relative paths are taken from the API folder, not the process working directory (differs under IIS).
            return Path.GetFullPath(Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(env.ContentRootPath, configured));
        }
    }
}
