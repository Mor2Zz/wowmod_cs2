using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Rogue
{
    public class RogueSinisterStrike : IActiveSpell
    {
        public string Id => "rogue.sinister_strike";
        public string Name => "Sinister Strike";
        public string Description => "Ближний удар: наносит 24 Physical урона ближайшей цели в прицеле.";

        private const string SpellId     = "rogue.sinister_strike";
        private const double ManaCost    = 0.0;
        private const double CooldownSec = 4.0;
        private const double DamageAmt   = 24.0;
        private const float  Range       = 250f;
        private const float  Fov         = 55f;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var sid = (ulong)player.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { rt.Print(player, "[Warcraft] Вы оглушены."); return false; }
            if (plugin.WowControl.IsSilenced(sid)) { rt.Print(player, "[Warcraft] Вы немые.");    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out var fail))
            { rt.Print(player, $"[Warcraft] {fail}."); return false; }

            var target = Targeting.TraceEnemyByView(player, Range, Fov);
            if (target is null || !target.IsValid) { rt.Print(player, "[Warcraft] Нет цели."); return false; }

            var tsid = (ulong)target.SteamID;
            plugin.WowApplyInstantDamage(sid, tsid, DamageAmt, DamageSchool.Physical);
            return true;
        }
    }
}