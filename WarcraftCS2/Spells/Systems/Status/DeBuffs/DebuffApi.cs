using System;
using CounterStrikeSharp.API.Core;

namespace WarcraftCS2.Spells.Systems.Status;

    /// <summary>Единая точка навешивания негативных эффектов с учётом иммунитетов.</summary>
    public static class DebuffApi
    {
        private static bool ImmuneToMovement(ulong sid) =>
            Buffs.Has(sid, "paladin.freedom.immune_slow")
         || Buffs.Has(sid, "paladin.freedom.immune_snare")
         || Buffs.Has(sid, "paladin.freedom.immune_root");

        private static bool ImmuneToStun(ulong sid)    => Buffs.Has(sid, "paladin.freedom.immune_stun");
        private static bool ImmuneToSilence(ulong sid) => Buffs.Has(sid, "paladin.freedom.immune_silence");
        private static bool ImmuneToPoison(ulong sid)  =>
            Buffs.Has(sid, "paladin.freedom.immune_poison") || Buffs.Has(sid, "paladin.cleanse.immune_poison");

        private static bool TryApply(ulong sid, string key, TimeSpan dur)
        { if (dur <= TimeSpan.Zero) return false; Buffs.Add(sid, key, dur); return true; }

        // движение
        public static bool TryApplySlow(CCSPlayerController target, TimeSpan dur, string src)    { var sid = target.SteamID; if (ImmuneToMovement(sid)) return false; return TryApply(sid, $"slow.{src}", dur); }
        public static bool TryApplyRoot(CCSPlayerController target, TimeSpan dur, string src)    { var sid = target.SteamID; if (ImmuneToMovement(sid)) return false; return TryApply(sid, $"root.{src}", dur); }
        public static bool TryApplySnare(CCSPlayerController target, TimeSpan dur, string src)   { var sid = target.SteamID; if (ImmuneToMovement(sid)) return false; return TryApply(sid, $"snare.{src}", dur); }

        // контроль
        public static bool TryApplyStun(CCSPlayerController target, TimeSpan dur, string src)    { var sid = target.SteamID; if (ImmuneToStun(sid))    return false; return TryApply(sid, $"stun.{src}", dur); }
        public static bool TryApplySilence(CCSPlayerController target, TimeSpan dur, string src) { var sid = target.SteamID; if (ImmuneToSilence(sid)) return false; return TryApply(sid, $"silence.{src}", dur); }

        // эффекты/DoT
        public static bool TryApplyPoison(CCSPlayerController target, TimeSpan dur, string src)  { var sid = target.SteamID; if (ImmuneToPoison(sid))  return false; return TryApply(sid, $"poison.{src}", dur); }
        public static bool TryApplyBleed(CCSPlayerController target, TimeSpan dur, string src)   => TryApply(target.SteamID, $"bleed.{src}",  dur);
        public static bool TryApplyIgnite(CCSPlayerController target, TimeSpan dur, string src)  => TryApply(target.SteamID, $"ignite.{src}", dur);
        public static bool TryApplyFear(CCSPlayerController target, TimeSpan dur, string src)    => TryApply(target.SteamID, $"fear.{src}",   dur);
        public static bool TryApplyBlind(CCSPlayerController target, TimeSpan dur, string src)   => TryApply(target.SteamID, $"blind.{src}",  dur);
        public static bool TryApplyDisarm(CCSPlayerController target, TimeSpan dur, string src)  => TryApply(target.SteamID, $"disarm.{src}", dur);
    }
