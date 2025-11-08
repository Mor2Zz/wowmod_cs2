using System;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using wowmod_cs2;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Status;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using wowmod_cs2.Features;

namespace WarcraftCS2.Spells.Classes.Warrior
{
    public sealed class Warbringer : IActiveSpell
    {
        public string Id => "warrior.warbringer";
        public string Name => "Warbringer";
        public string Description => "Агрессивный заход: урон + короткий стан по цели в прицеле.";

        private const double ManaCost    = 20.0;
        private const double CooldownSec = 14.0;
        private const float  Range       = 600f;
        private const float  FovDeg      = 35f;
        private const double HitDamage   = 35.0;
        private const double StunSecBase = 0.50;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> EarthsplitIcd = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> WarpathIcd = new();

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin) return false;
            var sid = (ulong)player.SteamID;
            var ctx = plugin.GetWowCombatContext();

            var prof = plugin.GetProfile(player);
            var aug  = Augments.GetSelected(prof, Id);

            double cooldown = CooldownSec;
            double stunSec  = StunSecBase;
            double damage   = HitDamage;

            if (aug == "skullcracker") stunSec += 0.30;

            if (!CastGate.TryBeginCast(ctx, sid, Id, ManaCost, cooldown, out var reason))
            { player.PrintToChat("[Warcraft] " + reason); return false; }

            var target = Targeting.TraceEnemyByView(player, Range, FovDeg);
            if (target is null || !target.IsValid) { player.PrintToChat("[Warcraft] Нет цели."); return false; }

            var tsid = (ulong)target.SteamID;

            plugin.WowApplyInstantDamage(sid, tsid, damage, DamageSchool.Physical);
            DebuffApi.TryApplyStun(target, TimeSpan.FromSeconds(stunSec), src: Id);

            if (aug == "skullcracker")
            {
                Buffs.Add(tsid, "warrior.demoralizing_shout.20", TimeSpan.FromSeconds(4));
            }

            if (aug == "earthsplit")
            {
                var now = DateTime.UtcNow;
                if (!EarthsplitIcd.TryGetValue(tsid, out var until) || now >= until)
                {
                    EarthsplitIcd[tsid] = now.AddSeconds(4);
                    var pawn = target.PlayerPawn?.Value;
                    var pos  = pawn?.AbsOrigin;

                    if (pos is not null)
                    {
                        void DoWave()
                        {
                            var center = pos!;
                            foreach (var enemy in Utilities.GetPlayers().Where(p => p is { IsValid: true } && p.TeamNum != player.TeamNum))
                            {
                                var ep = enemy.PlayerPawn?.Value;
                                if (ep is not { IsValid: true }) continue;
                                var d = Dist2D(center, ep.AbsOrigin!);
                                if (d > 250f) continue;

                                plugin.WowApplyInstantDamage(sid, (ulong)enemy.SteamID, damage * 0.25, DamageSchool.Physical);
                            }
                        }

                        plugin.AddTimer(0.5f, DoWave);
                        plugin.AddTimer(1.0f, DoWave);
                    }
                }
            }

            if (aug == "warpath")
            {
                var now = DateTime.UtcNow;
                if (!WarpathIcd.TryGetValue(sid, out var until) || now >= until)
                {
                    WarpathIcd[sid] = now.AddSeconds(8);

                    int countNear = 1;
                    var tpawn = target.PlayerPawn?.Value;
                    var tpos  = tpawn?.AbsOrigin;
                    if (tpos is not null)
                    {
                        foreach (var e in Utilities.GetPlayers().Where(p => p is { IsValid: true } && p.TeamNum != player.TeamNum && p != target))
                        {
                            var ep = e.PlayerPawn?.Value;
                            if (ep is not { IsValid: true }) continue;
                            if (Dist2D(tpos!, ep.AbsOrigin!) <= 250f) countNear++;
                            if (countNear >= 2) break;
                        }
                    }

                    if (countNear >= 2)
                    {
                        // возврат 40% КД
                        ctx.Cooldowns.Start(sid, Id, CooldownSec * 0.60);

                        // тег «хаст +20% на 2с» (если у тебя есть скорость — считай по этому ключу)
                        Buffs.Add(sid, "warrior.warbringer.warpath.haste20", TimeSpan.FromSeconds(2.0));
                    }
                }
            }

            player.PrintToChat("[Warcraft] Warbringer!");
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