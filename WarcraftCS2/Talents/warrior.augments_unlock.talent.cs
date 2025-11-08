using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using wowmod_cs2; // PlayerProfile
using WarcraftCS2.Gameplay; // ITalent, IWowRuntime
using wowmod_cs2.Features;

namespace WarcraftCS2.Talents
{
    /// Талант, открывающий ВСЕ аугменты для класса Warrior.
    public sealed class WarriorAugmentsUnlock : ITalent
    {
        public string Id => "warrior.augments.unlock";
        public string Name => "Путь воина";
        public string ClassId => "warrior";
        public int MinLevel => 100; 
        public string Description => "Открывает модификаторы (аугменты) умений Воина.";

        public void ApplyOnSpawn(IWowRuntime rt, CCSPlayerController player, PlayerProfile profile)
        {
            // Разрешаем аугменты для класса warrior
            Augments.GrantUnlockForClass(profile, ClassId);
            rt.Save();
        }

        public void OnPlayerHurt(IWowRuntime rt, EventPlayerHurt e,
                                 CCSPlayerController victim, CCSPlayerController? attacker,
                                 PlayerProfile profile) { }
    }
}