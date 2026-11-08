namespace FriendDex.Web.Services;

// Loads KEY=value lines from the nearest .env file (current folder or a parent)
// into environment variables. Variables that are already set are left alone.
public static class DotEnv
{
    public static void Load(string fileName = ".env", string? startDir = null)
    {
        var path = Find(fileName, startDir ?? Directory.GetCurrentDirectory());
        if (path == null) return;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ")) line = line["export ".Length..].TrimStart();

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
            {
                value = value[1..^1];
            }

            // Skip empty values so a blank placeholder doesn't hide a key set elsewhere.
            if (value.Length == 0) continue;
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string? Find(string fileName, string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        for (var i = 0; dir != null && i < 4; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
