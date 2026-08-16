namespace Alexandria.Formats.Scripts;

public static class SceneIDs
{
    public static IReadOnlyDictionary<String, Int32> Load(String path)
        => File.ReadAllLines(path)
            .Where(l => l.Length > 0)
            .Select(l => l.Split(';'))
            .ToDictionary(f => f[0], f => Int32.Parse(f[1]));
}

