using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    public static class Renew
    {
        private const string SpellId     = "priest_renew";
        private const double ManaCost    = 16.0;
        private const double CooldownSec = 4.0;
        private const double Tick        = 6.0;
        private const double Interval    = 1.0;
        private const double Duration    = 10.0;

        public static bool TryCast(WowmodCs2 plugin, CCSPlayerController caster, out string failReason)
        {
            failReason = string.Empty;
            if (plugin is null || caster is null || !caster.IsValid) { failReason = "Некорректный кастер"; return false; }

            var sid = (ulong)caster.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { failReason = "Вы оглушены"; return false; }
            if (plugin.WowControl.IsSilenced(sid)) { failReason = "Вы немые";    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out failReason))
                return false;

            var ally = Targeting.TraceAllyByView(caster, 900f, 45f, includeSelf: true) ?? caster;
            if (ally is null || !ally.IsValid) { failReason = "Нет цели"; return false; }
            var tsid = (ulong)ally.SteamID;

            plugin.WowAuras.AddOrRefresh(tsid, "priest.renew", AuraCategory.Magic, Duration, sid, magnitude: Tick);
            plugin.WowPeriodic.AddOrRefreshHot(sid, tsid, "priest.renew", Tick, Interval, Duration, addStacks: 0, maxStacks: 1);

            return true;
        }
    }

    public class PriestRenewActive : WarcraftCS2.Gameplay.IActiveSpell
    {
        public string Id => "priest.renew";
        public string Name => "Renew";
        public string Description => "HoT: периодическое исцеление цели.";

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            if (Renew.TryCast(plugin, player, out var reason))
                return true;

            rt.Print(player, $"[Warcraft] Renew: {reason}.");
            return false;
        }
    }
}