using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using wowmod_cs2;        // PlayerProfile
using WarcraftCS2.Gameplay;   // ITalent, IWowRuntime
using WarcraftCS2.Spells.Systems.Data;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Talents
{
    public class PaladinArmor15 : ITalent
    {
        public string Id => "paladin.armor.15";
        public string Name => "Стойкость света";
        public string ClassId => "paladin";
        public int MinLevel => 15;
        public string Description => "Повышает броню (демо).";

        public void ApplyOnSpawn(IWowRuntime rt, CCSPlayerController player, PlayerProfile profile) { }

        public void OnPlayerHurt(IWowRuntime rt, EventPlayerHurt e,
                                 CCSPlayerController victim, CCSPlayerController? attacker,
                                 PlayerProfile profile) { }
    }

    public class PaladinHp50 : ITalent
    {
        public string Id => "paladin.hp.50";
        public string Name => "Благословение";
        public string ClassId => "paladin";
        public int MinLevel => 50;
        public string Description => "Повышает запас здоровья (демо).";

        public void ApplyOnSpawn(IWowRuntime rt, CCSPlayerController player, PlayerProfile profile) { }

        public void OnPlayerHurt(IWowRuntime rt, EventPlayerHurt e,
                                 CCSPlayerController victim, CCSPlayerController? attacker,
                                 PlayerProfile profile) { }
    }

    public class PaladinReduceDamage100 : ITalent
    {
        public string Id => "paladin.reducedmg.100";
        public string Name => "Божественный щит";
        public string ClassId => "paladin";
        public int MinLevel => 100;
        public string Description => "Сильно снижает получаемый урон (демо).";

        public void ApplyOnSpawn(IWowRuntime rt, CCSPlayerController player, PlayerProfile profile) { }

        public void OnPlayerHurt(IWowRuntime rt, EventPlayerHurt e,
                                 CCSPlayerController victim, CCSPlayerController? attacker,
                                 PlayerProfile profile) { }
    }
}
