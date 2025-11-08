using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Hunter
{
    public class HunterSerpentSting : IActiveSpell
    {
        public string Id => "hunter.serpent_sting";
        public string Name => "Serpent Sting";
        public string Description => "DoT: 5 Nature урона/сек на 12с.";

        private const string SpellId     = "hunter.serpent_sting";
        private const double ManaCost    = 16.0;
        private const double CooldownSec = 6.0;
        private const double DurationSec = 12.0;
        private const double IntervalSec = 1.0;
        private const double TickDamage  = 5.0;

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

            // маркируем ауру (магическая для диспела)
            plugin.WowAuras.AddOrRefresh(tsid, SpellId, AuraCategory.Magic, DurationSec, sid);

            // собственно DoT
            plugin.WowPeriodic.AddOrRefreshDot(
                casterSid: sid,
                targetSid: tsid,
                id:        SpellId,
                school:    DamageSchool.Nature,
                amountPerTick: TickDamage,
                intervalSec:   IntervalSec,
                durationSec:   DurationSec,
                addStacks: 0,
                maxStacks: 1
            );

            return true;
        }
    }
}