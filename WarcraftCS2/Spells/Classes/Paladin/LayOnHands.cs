using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public static class LayOnHands
    {
        private const string SpellId     = "pal_lay_on_hands";
        private const double ManaCost    = 0.0;     // особый ресурс — оставляем 0
        private const double CooldownSec = 600.0;   // большой КД
        private const double HealAmount  = 120.0;   // сильный хил

        public static bool TryCast(WowmodCs2 plugin, CCSPlayerController caster, out string failReason)
        {
            failReason = string.Empty;
            if (plugin is null || caster is null || !caster.IsValid) { failReason = "Некорректный кастер"; return false; }

            var sid = (ulong)caster.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { failReason = "Оглушены"; return false; }
            if (plugin.WowControl.IsSilenced(sid)) { failReason = "Немой";     return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out failReason))
                return false;

            var ally = Targeting.TraceAllyByView(caster, 900f, 45f, includeSelf: true) ?? caster;
            if (ally is null || !ally.IsValid) { failReason = "Нет цели"; return false; }
            var tsid = (ulong)ally.SteamID;

            plugin.WowApplyInstantHeal(sid, tsid, HealAmount);
            return true;
        }
    }

    public class PaladinLayOnHandsActive : IActiveSpell
    {
        public string Id => "paladin.lay_on_hands";
        public string Name => "Lay on Hands";
        public string Description => "Сильное мгновенное исцеление цели (большой КД).";

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            if (LayOnHands.TryCast(plugin, player, out var reason))
                return true;

            rt.Print(player, $"[Warcraft] Lay on Hands: {reason}.");
            return false;
        }
    }
}