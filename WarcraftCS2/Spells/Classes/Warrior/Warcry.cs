using System;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using wowmod_cs2;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status;
using wowmod_cs2.Features;

namespace WarcraftCS2.Spells.Classes.Warrior
{
    public sealed class Warcry : IActiveSpell
    {
        public string Id => "warrior.warcry";
        public string Name => "Warcry";
        public string Description => "Боевой клич: бафф союзникам рядом и ослабление врагов.";

        private const double ManaCost    = 15.0;
        private const double CooldownSec = 14.0;
        private const float  Radius      = 500f;
        private const double DurationSec = 6.0;
        private const double Magnitude   = 1.05;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> EchoIcd = new();

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin) return false;
            var sid = (ulong)player.SteamID;
            var ctx = plugin.GetWowCombatContext();

            var prof = plugin.GetProfile(player);
            var aug  = Augments.GetSelected(prof, Id);

            if (!CastGate.TryBeginCast(ctx, sid, Id, ManaCost, CooldownSec, out var reason))
            { player.PrintToChat("[Warcraft] " + reason); return false; }

            var me = player.PlayerPawn?.Value;
            if (me is not { IsValid: true }) { player.PrintToChat("[Warcraft] Недоступно."); return false; }
            var myPos = me.AbsOrigin!;

            int buffed = 0, debuffed = 0;

            foreach (var ally in Utilities.GetPlayers().Where(p => p is { IsValid: true } && p.TeamNum == player.TeamNum))
            {
                var pawn = ally.PlayerPawn?.Value;
                if (pawn is not { IsValid: true }) continue;

                var d = Dist2D(myPos, pawn.AbsOrigin!);
                if (d > Radius) continue;

                plugin.WowAuras.AddOrRefresh(
                    targetSid: (ulong)ally.SteamID,
                    auraId: "warrior.warcry.buff",
                    categories: AuraCategory.None,
                    durationSec: DurationSec,
                    sourceSid: sid,
                    addStacks: 1,
                    maxStacks: 1,
                    mode: AuraRefreshMode.RefreshDuration_KeepStacks,
                    magnitude: Magnitude
                );
                buffed++;
            }

            double demoDur = 4.0;
            if (aug == "commanding_shout") demoDur = 5.0;

            foreach (var enemy in Utilities.GetPlayers().Where(p => p is { IsValid: true } && p.TeamNum != player.TeamNum))
            {
                var pawn = enemy.PlayerPawn?.Value;
                if (pawn is not { IsValid: true }) continue;

                var d = Dist2D(myPos, pawn.AbsOrigin!);
                if (d > Radius) continue;

                Buffs.Add((ulong)enemy.SteamID, "warrior.demoralizing_shout.20", TimeSpan.FromSeconds(demoDur));
                debuffed++;
            }

            if (aug == "terrifying_bellow")
            {
                foreach (var enemy in Utilities.GetPlayers().Where(p => p is { IsValid: true } && p.TeamNum != player.TeamNum))
                {
                    var pawn = enemy.PlayerPawn?.Value;
                    if (pawn is not { IsValid: true }) continue;

                    var d = Dist2D(myPos, pawn.AbsOrigin!);
                    if (d > Radius) continue;

                    plugin.WowAuras.AddOrRefresh(
                        targetSid: (ulong)enemy.SteamID,
                        auraId: "warrior.warcry.fear",
                        categories: AuraCategory.Stun,
                        durationSec: 0.8,
                        sourceSid: sid,
                        addStacks: 1,
                        maxStacks: 1,
                        mode: AuraRefreshMode.RefreshDuration_KeepStacks,
                        magnitude: 1.0
                    );
                }
            }

            if (aug == "echo")
            {
                plugin.AddTimer(1.5f, () =>
                {
                    foreach (var enemy in Utilities.GetPlayers().Where(p => p is { IsValid: true } && p.TeamNum != player.TeamNum))
                    {
                        var eid = (ulong)enemy.SteamID;

                        var pawn = enemy.PlayerPawn?.Value;
                        if (pawn is not { IsValid: true }) continue;

                        var d = Dist2D(myPos, pawn.AbsOrigin!);
                        if (d > Radius) continue;

                        var now = DateTime.UtcNow;
                        if (EchoIcd.TryGetValue(eid, out var until) && now < until) continue;
                        EchoIcd[eid] = now.AddSeconds(8);

                        plugin.WowAuras.AddOrRefresh(
                            targetSid: eid,
                            auraId: "warrior.warcry.echo.fear",
                            categories: AuraCategory.Stun,
                            durationSec: 0.4,
                            sourceSid: sid,
                            addStacks: 1, maxStacks: 1, mode: AuraRefreshMode.RefreshDuration_KeepStacks,
                            magnitude: 1.0
                        );

                        Buffs.Add(eid, "warrior.demoralizing_shout.20", TimeSpan.FromSeconds(4));
                    }
                });
            }

            player.PrintToChat($"[Warcraft] Warcry: союзников {buffed}, врагов ослаблено {debuffed}.");
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