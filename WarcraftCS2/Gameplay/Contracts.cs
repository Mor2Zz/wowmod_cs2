using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using wowmod_cs2;              
using wowmod_cs2.Config;       

namespace WarcraftCS2.Gameplay;

    // Рантайм, доступный талантам/спеллам
    public interface IWowRuntime
    {
        WowConfig Config { get; }
        System.Random Rng { get; }
        PlayerProfile GetProfile(CCSPlayerController player);
        void Print(CCSPlayerController player, string msg);
        void Save();
    }

    // Класс персонажа (Warrior/Paladin/Mage)
    public interface IWowClass
    {
        string Id { get; }
        string Name { get; }
        IReadOnlyList<string> ActiveSpells { get; }
        IReadOnlyList<string> InnateTalents { get; }
    }

    // Активный спелл
    public interface IActiveSpell
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        bool OnCast(IWowRuntime rt, CCSPlayerController player);
    }

    // Талант — сигнатуры
    public interface ITalent
    {
        string Id { get; }
        string Name { get; }
        string ClassId { get; }     // для какого класса
        int MinLevel { get; }       // минимальный уровень
        string Description { get; }

        // вызывается при спавне владельца таланта
        void ApplyOnSpawn(IWowRuntime rt, CCSPlayerController player, PlayerProfile profile);

        // вызывается при событии урона
        void OnPlayerHurt(IWowRuntime rt, EventPlayerHurt e,
                          CCSPlayerController victim, CCSPlayerController? attacker,
                          PlayerProfile profile);
    }