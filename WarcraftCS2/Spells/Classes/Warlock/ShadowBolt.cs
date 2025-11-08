using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage; // DamageSchool
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Warlock
{
    /// Shadow Bolt — мгновенный шэдоу-дамаг по цели в прицеле.
    public class WarlockShadowBolt : IActiveSpell
    {
        public string Id => "warlock.shadow_bolt";
        public string Name => "Shadow Bolt";
        public string Description => "Наносит 30 Shadow урона цели в прицеле.";

        private const string SpellId     = "warlock.shadow_bolt";
        private const double ManaCost    = 18.0;
        private const double CooldownSec = 6.0;
        private const double DamageAmt   = 30.0;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var sid = (ulong)player.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { rt.Print(player, "[Warcraft] Вы оглушены."); return false; }
            if (plugin.WowControl.IsSilenced(sid)) { rt.Print(player, "[Warcraft] Вы немые.");    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out var fail))
            { rt.Print(player, $"[Warcraft] {fail}."); return false; }

            var target = Targeting.TraceEnemyByView(player, 900f, 45f);
            if (target is null || !target.IsValid) { rt.Print(player, "[Warcraft] Нет цели."); return false; }

            var tsid = (ulong)target.SteamID;
            plugin.WowApplyInstantDamage(sid, tsid, DamageAmt, DamageSchool.Shadow);
            return true;
        }
    }
}