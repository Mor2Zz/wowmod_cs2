using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    public static class PowerWordShield
    {
        private const string SpellId      = "priest_power_word_shield";
        private const double ManaCost     = 20.0;
        private const double CooldownSec  = 6.0;

        private const double AbsorbAmount = 60.0;
        private const double DurationSec  = 8.0;

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

            // фактический щит + аура-метка
            plugin.WowShields.Add(tsid, AbsorbAmount, DurationSec, SpellId, priority: 2);
            plugin.WowAuras.AddOrRefresh(tsid, SpellId, AuraCategory.Magic, DurationSec, sid);

            return true;
        }
    }

    public class PriestPowerWordShieldActive : IActiveSpell
    {
        public string Id => "priest.power_word_shield";
        public string Name => "Power Word: Shield";
        public string Description => "Накладывает щит (абсорб) на цель на короткое время.";

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            if (PowerWordShield.TryCast(plugin, player, out var reason))
                return true;

            rt.Print(player, $"[Warcraft] PW: Shield: {reason}.");
            return false;
        }
    }
}