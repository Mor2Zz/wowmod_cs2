using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using wowmod_cs2;        // PlayerProfile
using WarcraftCS2.Gameplay;   // ITalent, IWowRuntime
using WarcraftCS2.Spells.Systems.Data;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Talents
{
    // Заглушка под "ignite" из старого мода — ничего не делает, просто существует
    public class MageIgnite : ITalent
    {
        public string Id => "mage.ignite.15";
        public string Name => "Возгорание (заглушка)";
        public string ClassId => "mage";
        public int MinLevel => 15;
        public string Description => "Временная заглушка таланта.";

        public void ApplyOnSpawn(IWowRuntime rt, CCSPlayerController player, PlayerProfile profile) { }

        public void OnPlayerHurt(IWowRuntime rt, EventPlayerHurt e,
                                 CCSPlayerController victim, CCSPlayerController? attacker,
                                 PlayerProfile profile) { }
    }
}