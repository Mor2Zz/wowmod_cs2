using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Rogue
{
    public class RogueGouge : IActiveSpell
    {
        public string Id => "rogue.gouge";
        public string Name => "Gouge";
        public string Description => "Короткий контроль (стан) цели в упоре.";

        private const string SpellId     = "rogue.gouge";
        private const double ManaCost    = 0.0;
        private const double CooldownSec = 12.0;
        private const double DurationSec = 3.0;
        private const float  Range       = 180f;
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
            plugin.WowAuras.AddOrRefresh(tsid, SpellId, AuraCategory.Stun, DurationSec, sid);
            return true;
        }
    }
}