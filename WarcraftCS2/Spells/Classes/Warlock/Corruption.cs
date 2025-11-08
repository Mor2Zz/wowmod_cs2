using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage;             // DamageSchool
using WarcraftCS2.Spells.Systems.Damage.Services;    // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Warlock
{
    /// Corruption — DoT: урон Тьмой каждый тик, +вешает магическую ауру на цели.
    public class WarlockCorruption : IActiveSpell
    {
        public string Id => "warlock.corruption";
        public string Name => "Corruption";
        public string Description => "DoT: 6 Shadow урона/сек на 10с.";

        private const string SpellId       = "warlock.corruption";
        private const double ManaCost      = 20.0;
        private const double CooldownSec   = 5.0;
        private const double DurationSec   = 10.0;
        private const double IntervalSec   = 1.0;
        private const double TickDamage    = 6.0;

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

            // 1) Магическая аура на цель (для диспела/инфо)
            plugin.WowAuras.AddOrRefresh(
                targetSid: tsid,
                auraId: SpellId,
                categories: AuraCategory.Magic,
                durationSec: DurationSec,
                sourceSid: sid
            );

            // 2) DoT с тиками раз в секунду
            plugin.WowPeriodic.AddOrRefreshDot(
                casterSid: sid,
                targetSid: tsid,
                id:        SpellId,
                school:    DamageSchool.Shadow,
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