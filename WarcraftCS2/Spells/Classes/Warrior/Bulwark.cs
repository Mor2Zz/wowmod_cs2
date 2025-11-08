using System;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using wowmod_cs2;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Damage.Services;
using wowmod_cs2.Features;

namespace WarcraftCS2.Spells.Classes.Warrior
{
    public sealed class Bulwark : IActiveSpell
    {
        public string Id => "warrior.bulwark";
        public string Name => "Bulwark";
        public string Description => "Защитный щит.";

        private const double ManaCost    = 20.0;
        private const double CooldownSec = 12.0;
        private const double ShieldBase  = 60.0;
        private const double ShieldDur   = 4.0;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin) return false;
            var sid = (ulong)player.SteamID;
            var ctx = plugin.GetWowCombatContext();

            var prof = plugin.GetProfile(player);
            var aug  = Augments.GetSelected(prof, Id);

            double cooldown = CooldownSec;
            double amount   = ShieldBase;
            double dur      = ShieldDur;

            switch (aug)
            {
                case "spellbreaker":
                    // маг-только абсорб 70 на 3с, вместо общего щита
                    amount = 70; dur = 3.0;
                    break;
                case "phalanx":
                    amount = 70; dur = 4.0;
                    break;
                case "riposte":
                    break;
            }

            if (!CastGate.TryBeginCast(ctx, sid, Id, ManaCost, cooldown, out var reason))
            { player.PrintToChat("[Warcraft] " + reason); return false; }

            if (aug == "spellbreaker")
            {
                plugin.AddMagicAbsorb(sid, amount, dur);
            }
            else
            {
                plugin.WowShields.Add(sid, amount, dur, source: Id, priority: 10);
            }

            if (aug == "phalanx")
            {
                var me = player.PlayerPawn?.Value;
                var pos = me?.AbsOrigin;
                if (pos is not null)
                {
                    foreach (var ally in Utilities.GetPlayers().Where(p => p is { IsValid: true } && p.TeamNum == player.TeamNum && p != player))
                    {
                        var pawn = ally.PlayerPawn?.Value;
                        if (pawn is not { IsValid: true }) continue;

                        var dx = (double)pawn.AbsOrigin!.X - pos!.X;
                        var dy = (double)pawn.AbsOrigin!.Y - pos!.Y;
                        var d2 = dx * dx + dy * dy;
                        if (d2 > 280.0 * 280.0) continue;

                        plugin.WowShields.Add((ulong)ally.SteamID, 25, 3.0, source: "warrior.bulwark.phalanx", priority: 4);
                    }
                }
            }

            if (aug == "riposte")
            {
                plugin.AddTimer(0.2f, () =>
                {
                    var target = WarcraftCS2.Spells.Systems.Core.Targeting.Targeting.TraceEnemyByView(player, 550f, 35f);
                    if (target is { IsValid: true })
                    {
                        plugin.WowApplyInstantDamage(sid, (ulong)target.SteamID, 30.0, DamageSchool.Physical);
                        WarcraftCS2.Spells.Systems.Status.DebuffApi.TryApplyStun(target, TimeSpan.FromSeconds(0.20), src: "warrior.bulwark.riposte");
                    }
                });
            }

            player.PrintToChat("[Warcraft] Bulwark!");
            return true;
        }
    }
}