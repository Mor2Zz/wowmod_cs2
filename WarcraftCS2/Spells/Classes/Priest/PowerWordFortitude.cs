using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    // Баф на союзника/себя. Пайплайн читает ауру и применяет свои эффекты.
    public class PriestPowerWordFortitude : IActiveSpell
    {
        public string Id => "priest.power_word_fortitude";
        public string Name => "Power Word: Fortitude";
        public string Description => "Благословение стойкости (магическая аура) на союзника/себя.";

        private const string SpellId      = "priest.power_word_fortitude";
        private const double ManaCost     = 16.0;
        private const double CooldownSec  = 8.0;
        private const double DurationSec  = 30.0;
        private const double MagnitudeAny = 1.0;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var sid = (ulong)player.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { rt.Print(player, "[Warcraft] Вы оглушены."); return false; }
            if (plugin.WowControl.IsSilenced(sid)) { rt.Print(player, "[Warcraft] Вы немые.");    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out var fail))
            { rt.Print(player, $"[Warcraft] {fail}."); return false; }

            var ally = Targeting.TraceAllyByView(player, 900f, 45f, includeSelf: true) ?? player;
            if (ally is null || !ally.IsValid) { rt.Print(player, "[Warcraft] Нет цели."); return false; }

            var tsid = (ulong)ally.SteamID;
            plugin.WowAuras.AddOrRefresh(tsid, SpellId, AuraCategory.Magic, DurationSec, sid, magnitude: MagnitudeAny);

            rt.Print(player, ally == player
                ? "[Warcraft] Fortitude на вас."
                : $"[Warcraft] Fortitude на {ally.PlayerName}.");

            return true;
        }
    }
}