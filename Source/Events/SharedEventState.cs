using System;
using System.Collections.Generic;

namespace wowmod_cs2;

public partial class WowmodCs2
{
    // Троттлинг для "шумных" событий (шаги/выстрелы), ключ — SteamID64 (Xuid)
    private readonly Dictionary<ulong, long> _footstepTs = new();
    private readonly Dictionary<ulong, long> _shootTs = new();

    /// <summary>Простой троттлинг по времени (мс) на ключ.</summary>
    private static bool Throttled(Dictionary<ulong, long> map, ulong key, int ms)
    {
        long now = Environment.TickCount64;
        if (map.TryGetValue(key, out var last) && (now - last) < ms) return true;
        map[key] = now;
        return false;
    }
}