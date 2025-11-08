using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;                           // IActiveSpell, IWowRuntime
using WarcraftCS2.Spells.Systems.Casting;             // CastGate
using WarcraftCS2.Spells.Systems.Core.Targeting;      // Targeting.TraceAllyByView
using WarcraftCS2.Spells.Systems.Damage;              // DamageSchool (не обяз., но оставлю для единообразия)
using WarcraftCS2.Spells.Systems.Damage.Services;     // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    /// Blessing of Kings — баф выживаемости (аура на союзника/себя).
    public class PaladinBlessingOfKings : IActiveSpell
    {
        public string Id => "paladin.blessing_kings";
        public string Name => "Blessing of Kings";
        public string Description => "10с: уменьшает входящий урон цели (~10%).";

        private const string SpellId     = "paladin.blessing_kings";
        private const double ManaCost    = 18.0;
        private const double CooldownSec = 14.0;
        private const double DurationSec = 10.0;

        // трактуем Magnitude как «величину эффекта» (проценты) — пайплайн читает как ему нужно
        private const double MagnitudePct = 10.0;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var sid = (ulong)player.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { rt.Print(player, "[Warcraft] Вы оглушены."); return false; }
            if (plugin.WowControl.IsSilenced(sid)) { rt.Print(player, "[Warcraft] Вы немые.");    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out var fail))
            { rt.Print(player, $"[Warcraft] {fail}."); return false; }

            // цель: союзник в прицеле, если нет — себя
            var target = Targeting.TraceAllyByView(player, 900f, 45f, includeSelf: true) ?? player;
            if (target is null || !target.IsValid) { rt.Print(player, "[Warcraft] Нет цели."); return false; }

            var tsid = (ulong)target.SteamID;

            // магическая аура с указанной длительностью и «силой» (Magnitude)
            plugin.WowAuras.AddOrRefresh(
                targetSid: tsid,
                auraId: SpellId,
                categories: AuraCategory.Magic,
                durationSec: DurationSec,
                sourceSid: sid,
                magnitude: MagnitudePct
            );

            // фидбек
            if (target == player) rt.Print(player, "[Warcraft] Blessing of Kings на вас.");
            else rt.Print(player, $"[Warcraft] Blessing of Kings на {target.PlayerName}.");

            return true;
        }
    }
}