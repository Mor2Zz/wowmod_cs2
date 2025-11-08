using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    public static class MindBlast
    {
        private const string SpellId     = "priest_mind_blast";
        private const double ManaCost    = 18.0;
        private const double CooldownSec = 8.0;
        private const double DamageAmt   = 30.0;

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

            var target = Targeting.TraceEnemyByView(caster, 900f, 45f);
            if (target is null || !target.IsValid) { failReason = "Нет цели"; return false; }

            var tsid = (ulong)target.SteamID;
            plugin.WowApplyInstantDamage(sid, tsid, DamageAmt, DamageSchool.Shadow);
            return true;
        }
    }

    public class PriestMindBlastActive : WarcraftCS2.Gameplay.IActiveSpell
    {
        public string Id => "priest.mind_blast";
        public string Name => "Mind Blast";
        public string Description => "Мгновенный урон Тьмой по цели в прицеле.";

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            if (MindBlast.TryCast(plugin, player, out var reason))
                return true;

            rt.Print(player, $"[Warcraft] Mind Blast: {reason}.");
            return false;
        }
    }
}