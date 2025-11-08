using System;
using System.IO;
using System.Text.Json;

namespace wowmod_cs2.Config
{
    public static class WowConfigLoader
    {
        public static WowConfig LoadOrDefault(string fullPath)
        {
            try
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(fullPath))
                {
                    var json = File.ReadAllText(fullPath);
                    return JsonSerializer.Deserialize<WowConfig>(json) ?? WowConfig.Default();
                }

                var cfg = WowConfig.Default();
                var jsonOut = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(fullPath, jsonOut);
                return cfg;
            }
            catch
            {
                return WowConfig.Default();
            }
        }
    }
}
