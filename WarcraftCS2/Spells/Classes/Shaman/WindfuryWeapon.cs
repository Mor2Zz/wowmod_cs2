using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Spells.Classes.Shaman
{
    public class ShamanWindfuryWeapon : IActiveSpell
    {
        public string Id => "shaman.windfury_weapon";
        public string Name => "Windfury Weapon";
        public string Description => "12с: on-hit даёт +8 Nature урона (общий ICD 0.25с).";

        private const double ManaCost = 14.0;
        private const double CooldownSec = 3.0;
        private const double DurationSec = 12.0;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not wowmod_cs2.WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var sid = (ulong)player.SteamID;
            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, Id, ManaCost, CooldownSec, out var reason))
            { rt.Print(player, $"[Warcraft] Windfury: {reason}."); return false; }

            plugin.WowAuras.AddOrRefresh(sid, Id, AuraCategory.Magic, DurationSec, sid);
            rt.Print(player, $"[Warcraft] Windfury активен {DurationSec:0}s.");
            return true;
        }
    }
}