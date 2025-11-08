using System;
using CounterStrikeSharp.API.Core;
using wowmod_cs2;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Status;
using WarcraftCS2.Spells.Systems.Status.Periodic;
using wowmod_cs2.Features;

namespace WarcraftCS2.Spells.Classes.Warrior
{
    public sealed class MortalStrike : IActiveSpell
    {
        public string Id => "warrior.mortal_strike";
        public string Name => "Mortal Strike";
        public string Description => "Мощный удар по цели; накладывает анти-хил.";

        private const double ManaCost    = 15.0;
        private const double CooldownSec = 7.0;
        private const double BaseDamage  = 48.0;
        private const double AntiHealPct = 0.50;
        private const double AntiHealDur = 6.0;
        private const float  Range       = 800f;
        private const float  FovDeg      = 40f;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin) return false;
            var sid = (ulong)player.SteamID;
            var ctx = plugin.GetWowCombatContext();

            var prof = plugin.GetProfile(player);
            var aug  = Augments.GetSelected(prof, Id);

            double cooldown = CooldownSec;
            double damage   = BaseDamage;
            double antiHeal = AntiHealPct;
            double antiDur  = AntiHealDur;

            switch (aug)
            {
                case "trauma":
                    antiHeal = 0.50;
                    antiDur  = 3.0;
                    break;
                case "ex_precision":
                    break;
                case "defiance":
                    damage *= 0.85;
                    // простой DR: +10% резистов на 2с по всем школам
                    foreach (DamageSchool s in Enum.GetValues(typeof(DamageSchool)))
                    {
                        var before = plugin.WowResists.GetPct(sid, s);
                        var after  = Math.Min(1.0, before + 0.10);
                        plugin.WowResists.SetPct(sid, s, after);
                        plugin.AddTimer(2.0f, () => plugin.WowResists.SetPct(sid, s, before));
                    }
                    break;
            }

            if (!CastGate.TryBeginCast(ctx, sid, Id, ManaCost, cooldown, out var reason))
            { player.PrintToChat("[Warcraft] " + reason); return false; }

            var target = Targeting.TraceEnemyByView(player, Range, FovDeg);
            if (target is null || !target.IsValid) { player.PrintToChat("[Warcraft] Нет цели."); return false; }
            var tsid = (ulong)target.SteamID;

            plugin.WowApplyInstantDamage(sid, tsid, damage, DamageSchool.Physical);

            plugin.WowAuras.AddOrRefresh(
                targetSid: tsid,
                auraId: "warrior.mortal_strike.antiheal",
                categories: AuraCategory.Magic,
                durationSec: antiDur,
                sourceSid: sid,
                addStacks: 1,
                maxStacks: 1,
                mode: AuraRefreshMode.RefreshDuration_KeepStacks,
                magnitude: antiHeal
            );

            if (aug == "trauma")
            {
                var before = plugin.WowResists.GetPct(tsid, DamageSchool.Physical);
                var after  = Math.Max(0.0, before - 0.15);
                plugin.WowResists.SetPct(tsid, DamageSchool.Physical, after);
                plugin.AddTimer(4.0f, () => plugin.WowResists.SetPct(tsid, DamageSchool.Physical, before));
            }

            if (aug == "ex_precision")
            {
                // Метка на 4с для рефанда КД по киллу
                plugin.AddMsExPrecisionMark(tsid, sid, 4.0);

                // Усиленный bleed 6с
                var bleedDps = Math.Max(1.0, damage * 0.25 * 1.25);
                plugin.WowPeriodic.AddOrRefreshDot(
                    casterSid: sid, targetSid: tsid, id: "warrior.mortal_strike.ex_precision",
                    school: DamageSchool.Physical,
                    amountPerTick: bleedDps, intervalSec: 1.0, durationSec: 6.0,
                    addStacks: 1, maxStacks: 1
                );
            }

            player.PrintToChat("[Warcraft] Mortal Strike!");
            return true;
        }
    }
}