using System.Text.Json;

namespace wowmod_cs2.Persistence;

public interface IStorage
{
    Task SaveAsync(string path, object data);
    Task<T?> LoadAsync<T>(string path);
}

public sealed class JsonStorage : IStorage
{
    public async Task SaveAsync(string path, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<T?> LoadAsync<T>(string path)
    {
        if (!File.Exists(path)) return default;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<T>(json);
    }
}
