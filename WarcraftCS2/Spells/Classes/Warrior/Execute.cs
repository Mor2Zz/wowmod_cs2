using System;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using wowmod_cs2;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status;
using wowmod_cs2.Features;

namespace WarcraftCS2.Spells.Classes.Warrior
{
    public sealed class Execute : IActiveSpell
    {
        public string Id => "warrior.execute";
        public string Name => "Execute";
        public string Description => "Добивание по низкому здоровью.";

        private const double ManaCost     = 20.0;
        private const double CooldownSec  = 6.0;
        private const float  Range        = 550f;
        private const float  FovDeg       = 35f;
        private const double BaseDamage   = 40.0;
        private const int    ThresholdPct = 25;
        private const double MaxMul       = 2.50;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin) return false;
            var sid = (ulong)player.SteamID;
            var ctx = plugin.GetWowCombatContext();

            var prof = plugin.GetProfile(player);
            var aug  = Augments.GetSelected(prof, Id);

            double cooldown = CooldownSec;
            double baseDmg  = BaseDamage;
            int    thresh   = ThresholdPct;

            switch (aug)
            {
                case "guillotine":
                    baseDmg *= 1.20; // +20% база
                    break;
                case "mercy_is_weakness":
                    thresh = 30; baseDmg *= 0.90;
                    break;
                case "deathmark":
                    break;
            }

            var target = Targeting.TraceEnemyByView(player, Range, FovDeg);
            if (target is null || !target.IsValid) { player.PrintToChat("[Warcraft] Нет цели."); return false; }
            var tsid = (ulong)target.SteamID;

            if (aug == "guillotine")
            {
                if (!CastGate.TryBeginCast(ctx, sid, Id, ManaCost, cooldown, out var reason))
                { player.PrintToChat("[Warcraft] " + reason); return false; }

                // Каст 0.4с; при сбитии — 60% урона
                CastScheduler.BeginCast(
                    plugin, player, Id, 0.40,
                    onComplete: () => DoExecute(plugin, player, tsid, baseDmg, thresh, full: true, addDeathmark: aug == "deathmark"),
                    onCancel:  _ => DoExecute(plugin, player, tsid, baseDmg * 0.60, thresh, full: false, addDeathmark: aug == "deathmark")
                );
                return true;
            }

            if (!CastGate.TryBeginCast(ctx, sid, Id, ManaCost, cooldown, out var reason2))
            { player.PrintToChat("[Warcraft] " + reason2); return false; }

            DoExecute(plugin, player, tsid, baseDmg, thresh, full: true, addDeathmark: aug == "deathmark");
            return true;
        }

        private void DoExecute(WowmodCs2 plugin, CCSPlayerController player, ulong tsid, double baseDmg, int thresh, bool full, bool addDeathmark)
        {
            var vic = Utilities.GetPlayers().FirstOrDefault(p => p is { IsValid: true } && (ulong)p.SteamID == tsid);
            if (vic is null) return;

            var pawn = vic.PlayerPawn?.Value;
            if (pawn is not { IsValid: true }) return;

            int pct = Math.Clamp(pawn.Health, 0, 100);
            if (pct > thresh) { player.PrintToChat($"[Warcraft] Цель слишком здорова ({pct}%). Порог {thresh}%."); return; }

            double t = Math.Clamp(1.0 - (pct / (double)thresh), 0.0, 1.0);
            double mul = 1.0 + (MaxMul - 1.0) * t;
            double dmg = baseDmg * mul;

            plugin.WowApplyInstantDamage((ulong)player.SteamID, tsid, dmg, DamageSchool.Physical);

            if (addDeathmark && full)
            {
                plugin.WowAuras.AddOrRefresh(
                    targetSid: tsid,
                    auraId: "warrior.execute.deathmark",
                    categories: AuraCategory.None,
                    durationSec: 3.0,
                    sourceSid: (ulong)player.SteamID,
                    addStacks: 1, maxStacks: 1, mode: AuraRefreshMode.RefreshDuration_KeepStacks,
                    magnitude: 1.0
                );
            }

            player.PrintToChat($"[Warcraft] Execute: ×{mul:0.00} урона{(full ? "" : " (сбит каст — 60%)")}.");
        }
    }
}