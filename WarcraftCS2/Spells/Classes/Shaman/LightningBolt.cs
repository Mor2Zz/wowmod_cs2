using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage;

namespace WarcraftCS2.Spells.Classes.Shaman
{
    public class ShamanLightningBolt : IActiveSpell
    {
        public string Id => "shaman.lightning_bolt";
        public string Name => "Lightning Bolt";
        public string Description => "Наносит 30 Nature урона цели в прицеле.";

        private const double ManaCost = 18.0;
        private const double CooldownSec = 6.0;
        private const double DamageAmt = 30.0;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not wowmod_cs2.WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var ctx = plugin.GetWowCombatContext();
            if (!Systems.Casting.CastGate.TryBeginCast(ctx, (ulong)player.SteamID, Id, ManaCost, CooldownSec, out var reason))
            { rt.Print(player, $"[Warcraft] Lightning Bolt: {reason}."); return false; }

            var target = Targeting.TraceEnemyByView(player);
            if (target is null || !target.IsValid) { rt.Print(player, "[Warcraft] Lightning Bolt: цель не найдена."); return false; }

            var ok = plugin.WowApplyInstantDamage((ulong)player.SteamID, (ulong)target.SteamID, DamageAmt, DamageSchool.Nature);
            rt.Print(player, ok ? "[Warcraft] Lightning Bolt: попадание!" : "[Warcraft] Lightning Bolt: цель защищена.");
            return ok;
        }
    }
}