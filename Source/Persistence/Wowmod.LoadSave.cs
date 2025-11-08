using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace wowmod_cs2
{
    public partial class WowmodCs2
    {
        private readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Чтение JSON В СУЩЕСТВУЮЩИЙ readonly-словарь (_profiles) — без присваивания
        private void LoadProfilesSafe()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_profilesJsonPath))
                {
                    Logger.LogError("[wowmod] profiles path is empty in config");
                    return;
                }

                var root = AppContext.BaseDirectory;
                var path = Path.Combine(root, _profilesJsonPath);
                var dir = Path.GetDirectoryName(path)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(path))
                {
                    Logger.LogInformation($"[wowmod] profiles file not found, creating empty: {path}");
                    lock (_profilesLock) _profiles.Clear();
                    return;
                }

                var json = File.ReadAllText(path);
                var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<ulong, WarcraftCS2.Gameplay.PlayerProfile>>(json)
                           ?? new Dictionary<ulong, WarcraftCS2.Gameplay.PlayerProfile>();

                lock (_profilesLock)
                {
                    _profiles.Clear();
                    foreach (var kv in data) _profiles[kv.Key] = kv.Value;
                }
                Logger.LogInformation($"[wowmod] loaded {_profiles.Count} player profiles");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[wowmod] LoadProfilesSafe failed");
            }
        }

        // Атомарная запись (tmp → replace)
        private void SaveProfilesSafe()
        {
            try
            {
                QueueProfilesSaveSnapshot(); // неблокирующее сохранение
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[wowmod] SaveProfilesSafe failed");
            }
        }
    }
}