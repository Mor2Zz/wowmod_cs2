using System;
using System.Collections.Generic;
using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Systems.Core.Runtime
{
    public static class SpellRunner
    {
        public static bool TryBeginAndResolveTargets(
            WowmodCs2 plugin,
            CCSPlayerController caster,
            object spellInstance,
            string spellId,
            double manaCost,
            double cooldownSec,
            out List<TargetSnapshot> targets,
            out string failReason)
        {
            targets = new List<TargetSnapshot>();
            failReason = string.Empty;

            if (plugin is null) { failReason = "Plugin is null"; return false; }
            if (caster is null || !caster.IsValid) { failReason = "Некорректный кастер"; return false; }

            var casterSid = (ulong)caster.SteamID;

            if (plugin.WowControl.IsStunned(casterSid))  { failReason = "Вы оглушены"; return false; }
            if (plugin.WowControl.IsSilenced(casterSid)) { failReason = "Вы немые";    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, casterSid, spellId, manaCost, cooldownSec, out failReason))
                return false;

            var casterSnap = MakeSnapshot(caster, isSelf: true);
            var candidates = CollectCandidates(caster);

            static bool Allies(int a, int b) => a == b;

            if (!TargetingGate.TryResolveTargets(
                    spellInstance,
                    casterSnap,
                    candidates,
                    Allies,
                    out targets,
                    out failReason,
                    hasLoS: (from, to) => true)) 
            {
                return false;
            }

            return true;
        }

        public static bool TryComputeSpellDamage(
            WowmodCs2 plugin,
            CCSPlayerController attacker,
            CCSPlayerController victim,
            double baseDamage,
            DamageSchool school,
            out double finalDamage,
            out double absorbed,
            out string failReason)
        {
            finalDamage = 0;
            absorbed = 0;
            failReason = string.Empty;

            if (plugin is null) { failReason = "Plugin is null"; return false; }
            if (attacker is null || !attacker.IsValid || victim is null || !victim.IsValid)
            { failReason = "Некорректные участники урона"; return false; }

            var services = plugin.GetWowDamageServices();
            if (services.Equals(default))
            { failReason = "Damage services not ready"; return false; }

            DamagePipeline? pipelineMaybe = services.Item1;
            if (pipelineMaybe is null)
            { failReason = "Damage pipeline is null"; return false; }

            var pipeline = pipelineMaybe;

            var atkSid = (ulong)attacker.SteamID;
            var vicSid = (ulong)victim.SteamID;

            string? reasonTmp;
            var ok = pipeline.ResolveIncoming(
                atkSid, vicSid, baseDamage, school,
                out finalDamage, out absorbed, out reasonTmp
            );

            failReason = reasonTmp ?? string.Empty;
            return ok;
        }

        // helpers
        private static TargetSnapshot MakeSnapshot(CCSPlayerController p, bool isSelf)
        {
            var snap = new TargetSnapshot
            {
                SteamId = (ulong)p.SteamID,
                Team = Convert.ToInt32(p.Team),
                Alive = true,
                IsSelf = isSelf,
                Position = default,
                Forward = default
            };

            var pawn = p.PlayerPawn?.Value;
            if (pawn != null && pawn.IsValid)
            {
                if (pawn.AbsOrigin is { } pos)
                    snap.Position = new Vector3(pos.X, pos.Y, pos.Z);

                if (pawn.EyeAngles is { } eye)
                    snap.Forward = AngleToForward((float)eye.X, (float)eye.Y);
            }

            return snap;
        }

        private static List<TargetSnapshot> CollectCandidates(CCSPlayerController caster)
        {
            var result = new List<TargetSnapshot>(32);
            foreach (var pl in Utilities.GetPlayers())
            {
                if (pl is null || !pl.IsValid) continue;
                int team = Convert.ToInt32(pl.Team);
                if (team <= 0) continue;
                result.Add(MakeSnapshot(pl, isSelf: pl == caster));
            }
            return result;
        }

        private static Vector3 AngleToForward(float pitchDeg, float yawDeg)
        {
            float pitch = pitchDeg * (MathF.PI / 180f);
            float yaw   = yawDeg   * (MathF.PI / 180f);
            float cp = MathF.Cos(pitch), sp = MathF.Sin(pitch);
            float cy = MathF.Cos(yaw),   sy = MathF.Sin(yaw);
            var fwd = new Vector3(cp * cy, cp * sy, -sp);
            if (fwd.LengthSquared() > 1e-6f) fwd = Vector3.Normalize(fwd);
            return fwd;
        }
    }
}