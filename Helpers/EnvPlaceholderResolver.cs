using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace EMISAPIS.Helpers
{
    /// <summary>
    /// Replaces <c>${ENV_NAME}</c> placeholders in configuration values with
    /// process environment variables (after <see cref="EnvFileLoader"/>).
    /// </summary>
    public static class EnvPlaceholderResolver
    {
        private static readonly Regex Placeholder = new(
            @"\$\{([A-Za-z0-9_.]+)\}",
            RegexOptions.Compiled);

        public static void Expand(IConfiguration configuration)
        {
            if (configuration is not IConfigurationRoot root)
                return;

            ExpandSection(root, root);
        }

        private static void ExpandSection(IConfigurationRoot root, IConfiguration section)
        {
            foreach (var child in section.GetChildren())
            {
                if (child.GetChildren().Any())
                {
                    ExpandSection(root, child);
                    continue;
                }

                var raw = child.Value;
                if (string.IsNullOrEmpty(raw) || !raw.Contains("${", StringComparison.Ordinal))
                    continue;

                var expanded = Placeholder.Replace(raw, match =>
                {
                    var key = match.Groups[1].Value;
                    var env = Environment.GetEnvironmentVariable(key);
                    if (!string.IsNullOrEmpty(env))
                        return env;

                    // Also allow colon form: OtpSms:Password ↔ OtpSms__Password
                    var alt = key.Replace(':', '_').Replace("__", "_");
                    if (key.Contains(':'))
                    {
                        var underscored = key.Replace(":", "__");
                        env = Environment.GetEnvironmentVariable(underscored);
                        if (!string.IsNullOrEmpty(env))
                            return env;
                    }

                    return match.Value;
                });

                if (!string.Equals(raw, expanded, StringComparison.Ordinal))
                    root[child.Path] = expanded;
            }
        }
    }
}
