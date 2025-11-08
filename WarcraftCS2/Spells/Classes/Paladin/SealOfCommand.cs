using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public class PaladinSealOfCommand : IActiveSpell
    {
        public string Id => "paladin.seal_command";
        public string Name => "Seal of Command";
        public string Description => "12с: Holy cleave по двум ближайшим врагам при попадании.";

        private const double ManaCost = 14.0;
        private const double CooldownSec = 3.0;
        private const double DurationSec = 12.0;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not wowmod_cs2.WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var sid = (ulong)player.SteamID;
            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, Id, ManaCost, CooldownSec, out var reason))
            { rt.Print(player, $"[Warcraft] Seal of Command: {reason}."); return false; }

            // Снимаем другие печати этого паладина
            plugin.WowAuras.Remove(sid, "paladin.seal_righteousness");

            // Вешаем/обновляем эту печать
            plugin.WowAuras.AddOrRefresh(sid, Id, AuraCategory.Magic, DurationSec, sid);
            rt.Print(player, $"[Warcraft] Seal of Command активна {DurationSec:0}s.");
            return true;
        }
    }
}