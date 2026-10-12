using System.Text.Json;

namespace EMISAPIS.Helpers
{
    /// <summary>
    /// Loads configuration from a local <c>.env</c> file into process environment variables.
    /// Supports both KEY=VALUE format and standard JSON format.
    /// </summary>
    public static class EnvFileLoader
    {
        public static void Load(string? contentRoot = null, string fileName = ".env")
        {
            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(contentRoot))
                candidates.Add(Path.Combine(contentRoot, fileName));

            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), fileName));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName));
            candidates.Add(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", fileName)));
            candidates.Add(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", fileName)));

            string? path = candidates.FirstOrDefault(File.Exists);
            if (path == null)
                return;

            var rawContent = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(rawContent))
                return;

            // 1. Try parsing KEY=VALUE lines
            var lines = File.ReadAllLines(path);
            bool hasKeyValue = false;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//"))
                    continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                hasKeyValue = true;
                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim();
                if (value.Length >= 2
                    && ((value.StartsWith('"') && value.EndsWith('"'))
                        || (value.StartsWith('\'') && value.EndsWith('\''))))
                {
                    value = value[1..^1];
                }

                if (!string.IsNullOrWhiteSpace(key))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            // 2. If JSON block is present in the file, also parse JSON
            var jsonStartIndex = rawContent.IndexOf('{');
            var jsonEndIndex = rawContent.LastIndexOf('}');
            if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
            {
                try
                {
                    var jsonSubstring = rawContent.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                    using var doc = JsonDocument.Parse(jsonSubstring, new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });
                    FlattenJsonElement(string.Empty, doc.RootElement);
                }
                catch
                {
                    // Ignore JSON parse errors if already handled via key-value
                }
            }
        }

        private static void FlattenJsonElement(string prefix, JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}__{prop.Name}";
                    FlattenJsonElement(key, prop.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenJsonElement($"{prefix}__{index++}", item);
                }
            }
            else
            {
                var val = element.ToString();
                if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(val))
                {
                    Environment.SetEnvironmentVariable(prefix, val);
                }
            }
        }
    }
}
