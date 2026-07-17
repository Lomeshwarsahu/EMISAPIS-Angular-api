namespace EMISAPIS.Helpers
{
    /// <summary>
    /// Loads KEY=VALUE pairs from a local <c>.env</c> file into process environment
    /// so <see cref="WebApplication.CreateBuilder"/> picks them up via configuration.
    /// </summary>
    public static class EnvFileLoader
    {
        public static void Load(string? contentRoot = null, string fileName = ".env")
        {
            var root = contentRoot
                ?? Directory.GetCurrentDirectory();
            var path = Path.Combine(root, fileName);
            if (!File.Exists(path))
            {
                // Also try project directory when running from bin/
                var alt = Path.GetFullPath(Path.Combine(root, "..", "..", "..", fileName));
                if (File.Exists(alt))
                    path = alt;
                else
                    return;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim();
                if (value.Length >= 2
                    && ((value.StartsWith('"') && value.EndsWith('"'))
                        || (value.StartsWith('\'') && value.EndsWith('\''))))
                {
                    value = value[1..^1];
                }

                if (string.IsNullOrWhiteSpace(key))
                    continue;

                // Do not override variables already set by the host / shell.
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    continue;

                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
