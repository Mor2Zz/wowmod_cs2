using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Warlock
{
    /// Fear — контроль на короткое время (как дизориентация/стан через категорию Stun).
    public class WarlockFear : IActiveSpell
    {
        public string Id => "warlock.fear";
        public string Name => "Fear";
        public string Description => "Дизориентация цели на 3.5с.";

        private const string SpellId     = "warlock.fear";
        private const double ManaCost    = 20.0;
        private const double CooldownSec = 12.0;
        private const double DurationSec = 3.5;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var sid = (ulong)player.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { rt.Print(player, "[Warcraft] Вы оглушены."); return false; }
            if (plugin.WowControl.IsSilenced(sid)) { rt.Print(player, "[Warcraft] Вы немые.");    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out var fail))
            { rt.Print(player, $"[Warcraft] {fail}."); return false; }

            var target = Targeting.TraceEnemyByView(player, 550f, 35f);
            if (target is null || !target.IsValid) { rt.Print(player, "[Warcraft] Нет цели."); return false; }

            var tsid = (ulong)target.SteamID;

            // Кладём стан-ауру «fear» — пайплайн ControlService трактует Stun как запрет движения/каста.
            plugin.WowAuras.AddOrRefresh(
                targetSid: tsid,
                auraId: "warlock.fear",
                categories: AuraCategory.Stun,
                durationSec: DurationSec,
                sourceSid: sid
            );

            return true;
        }
    }
}