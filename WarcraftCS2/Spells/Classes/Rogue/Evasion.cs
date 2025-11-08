using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Rogue
{
    public class RogueEvasion : IActiveSpell
    {
        public string Id => "rogue.evasion";
        public string Name => "Evasion";
        public string Description => "5с: повышает уклонение/снижение входящего урона.";

        private const string SpellId     = "rogue.evasion";
        private const double ManaCost    = 0.0;
        private const double CooldownSec = 25.0;
        private const double DurationSec = 5.0;
        private const double Magnitude   = 30.0; // трактуем как -30% входящего (или +эвейжн в пайплайне)

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var sid = (ulong)player.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { rt.Print(player, "[Warcraft] Вы оглушены."); return false; }
            if (plugin.WowControl.IsSilenced(sid)) { rt.Print(player, "[Warcraft] Вы немые.");    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out var fail))
            { rt.Print(player, $"[Warcraft] {fail}."); return false; }

            plugin.WowAuras.AddOrRefresh(sid, SpellId, AuraCategory.Magic, DurationSec, sid, magnitude: Magnitude);
            rt.Print(player, "[Warcraft] Evasion активна.");
            return true;
        }
    }
}