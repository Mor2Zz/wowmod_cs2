using System.Collections.Concurrent;

namespace RPG.XP;

public enum PlayerRole { Dps, Support, Tank }

public static class RoleSystem
{
    private static readonly ConcurrentDictionary<ulong, PlayerRole> _roles = new();

    public static void SetRole(ulong playerId, PlayerRole role) => _roles[playerId] = role;
    public static PlayerRole GetRole(ulong playerId) => _roles.TryGetValue(playerId, out var r) ? r : PlayerRole.Dps;
    public static void ClearPlayer(ulong playerId) => _roles.TryRemove(playerId, out _);
}