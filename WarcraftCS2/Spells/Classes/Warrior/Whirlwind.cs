using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using wowmod_cs2;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Status.Periodic;
using wowmod_cs2.Features;
using WarcraftCS2.Spells.Systems.Status;
using WarcraftCS2.Spells.Systems.Damage.Services;

namespace WarcraftCS2.Spells.Classes.Warrior
{
    public sealed class Whirlwind : IActiveSpell
    {
        public string Id => "warrior.whirlwind";
        public string Name => "Whirlwind";
        public string Description => "Сильный круговой удар по близким врагам с ослаблением урона по дальним целям.";

        private const double ManaCost    = 25.0;
        private const double CooldownSec = 8.0;
        private const float  Radius      = 260f;
        private const int    MaxTargets  = 5;
        private const double BaseDamage  = 45.0;
        private const double MinMul      = 0.35;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin) return false;
            var sid = (ulong)player.SteamID;

            var prof = plugin.GetProfile(player);
            var aug  = Augments.GetSelected(prof, Id);

            double cooldown = CooldownSec;
            double damageAtCenter = BaseDamage;
            float  radius = Radius;

            bool giveFreedom = false;
            bool tagSelfSlow = false;
            double freedomDur = 2.5;

            switch (aug)
            {
                case "bladestorm":
                    damageAtCenter *= 0.80; // −20% урон
                    giveFreedom = true;      // иммун к софт-CC на время
                    tagSelfSlow = true;      // пометим «самоснейр −35%» ключом
                    break;
                case "bloodstorm":
                    break;
                case "razor_guard":
                    break;
            }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, Id, ManaCost, cooldown, out var reason))
            { player.PrintToChat("[Warcraft] " + reason); return false; }

            var me = player.PlayerPawn?.Value;
            if (me is not { IsValid: true }) { player.PrintToChat("[Warcraft] Недоступно."); return false; }
            var myPos = me.AbsOrigin!;

            // выдаём freedom-ауру и optional self-slow-tag (если у тебя есть обработчик скорости — используй этот ключ)
            if (giveFreedom)
            {
                plugin.WowAuras.AddOrRefresh(
                    targetSid: sid,
                    auraId: "paladin.freedom",
                    categories: AuraCategory.None,
                    durationSec: freedomDur,
                    sourceSid: sid,
                    addStacks: 1, maxStacks: 1, mode: AuraRefreshMode.RefreshDuration_KeepStacks,
                    magnitude: 1.0
                );

                if (tagSelfSlow)
                {
                    // просто тегом: если где-то считаешь скорость — смотри этот ключ как −35%
                    WarcraftCS2.Spells.Systems.Status.Buffs.Add(sid, "warrior.whirlwind.bladestorm.selfslow35", TimeSpan.FromSeconds(freedomDur));
                }
            }

            var enemies = Utilities.GetPlayers()
                .Where(p => p is { IsValid: true } && p.TeamNum != player.TeamNum)
                .Select(p => (p, pawn: p.PlayerPawn?.Value))
                .Where(t => t.pawn is { IsValid: true })
                .Select(t => (t.p, pos: t.pawn!.AbsOrigin!))
                .Select(tp => (tp.p, dist: Dist2D(myPos, tp.pos)))
                .Where(x => x.dist <= radius)
                .OrderBy(x => x.dist)
                .Take(MaxTargets)
                .ToList();

            if (enemies.Count == 0) { player.PrintToChat("[Warcraft] Нет целей рядом."); return false; }

            int hitCount = 0;
            double totalBleedDps = 0.0;

            foreach (var (t, dist) in enemies)
            {
                var k = Math.Clamp(1.0 - (dist / radius), 0.0, 1.0);
                var mul = MinMul + (1.0 - MinMul) * k;
                var dmg = damageAtCenter * mul;

                plugin.WowApplyInstantDamage(sid, (ulong)t.SteamID, dmg, DamageSchool.Physical);
                hitCount++;

                if (aug == "bloodstorm")
                {
                    var bleedDps = dmg * 0.25 * 1.60;
                    totalBleedDps += bleedDps;

                    plugin.WowPeriodic.AddOrRefreshDot(
                        casterSid: sid,
                        targetSid: (ulong)t.SteamID,
                        id: "warrior.whirlwind.bleed",
                        school: DamageSchool.Physical,
                        amountPerTick: bleedDps, intervalSec: 1.0, durationSec: 4.0,
                        addStacks: 1, maxStacks: 1
                    );
                }
            }

            if (aug == "bloodstorm" && totalBleedDps > 0)
            {
                var leechPerSec = Math.Min(20.0, totalBleedDps * 0.15);
                plugin.WowPeriodic.AddOrRefreshHot(
                    casterSid: sid, targetSid: sid, id: "warrior.whirlwind.bloodstorm.leech",
                    amountPerTick: leechPerSec, intervalSec: 1.0, durationSec: 4.0,
                    addStacks: 1, maxStacks: 1
                );
            }

            if (aug == "razor_guard" && hitCount > 0)
            {
                var shield = Math.Min(40, hitCount * 8);
                plugin.WowShields.Add(sid, shield, 5.0, source: "warrior.whirlwind.razor_guard", priority: 5);
            }

            player.PrintToChat($"[Warcraft] Whirlwind: целей {enemies.Count}.");
            return true;
        }

        private static double Dist2D(CounterStrikeSharp.API.Modules.Utils.Vector a, CounterStrikeSharp.API.Modules.Utils.Vector b)
        {
            var dx = (double)a.X - b.X;
            var dy = (double)a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}