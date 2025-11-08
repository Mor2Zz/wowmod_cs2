using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Status;
using WarcraftCS2.Spells.Systems.Data;
using RPG.XP;

namespace wowmod_cs2
{
    public partial class WowmodCs2
    {
        // Зарегистрировано в Load():
        // RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawned, HookMode.Post)
        private HookResult OnPlayerSpawned(EventPlayerSpawn @event, GameEventInfo info)
        {
            try
            {
                var player = @event.Userid; // в CS2 это уже CCSPlayerController
                if (player is null || !player.IsValid) return HookResult.Continue;

                var prof = GetOrCreateProfile(player);

                // Если у игрока выбран класс — назначим роль по атрибуту класса
                if (!string.IsNullOrWhiteSpace(prof.ClassId) &&
                    WowRegistry.Classes.TryGetValue(prof.ClassId, out var cls) && cls is not null)
                {
                    var t = cls.GetType();
                    var attr = t.GetCustomAttribute<WowRoleAttribute>();
                    var role = attr?.Role ?? PlayerRole.Dps; // по умолчанию DPS
                    RoleSystem.SetRole(player.SteamID, role);
                }

                // Пассивки/таланты активируются через реестр
                WowRegistry.DispatchSpawn(this, player);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[wowmod] OnPlayerSpawned failed");
            }

            return HookResult.Continue;
        }
    }
}