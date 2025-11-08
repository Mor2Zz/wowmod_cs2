using System.Collections.Generic;

namespace WarcraftCS2.Spells.Systems.Data
{
    /// <summary>Простая локализация названий заклинаний (RU). Если ключа нет — используем английское имя.</summary>
    public static class SpellTitlesRu
    {
        private static readonly Dictionary<string, string> _map = new()
        {
            // Warrior
            ["warrior.battle_shout"] = "Боевой клич",
            ["warrior.berserker_rage"] = "Ярость берсерка",
            ["warrior.cleave"] = "Рассечение",
            ["warrior.demoralizing_shout"] = "Деморализующий крик",
            ["warrior.intimidating_shout"] = "Устрашающий крик",
            ["warrior.heroic_strike"] = "Удар героя",
            ["warrior.mortal_strike"] = "Смертельный удар",
            ["warrior.rend"] = "Рваная рана",
            ["warrior.slam"] = "Сокрушение",
            ["warrior.execute"] = "Казнь",
            ["warrior.overpower"] = "Превосходство",
            ["warrior.revenge"] = "Реванш",
            ["warrior.thunder_clap"] = "Громовой раскат",
            ["warrior.whirlwind"] = "Вихрь клинков",
            ["warrior.shield_block"] = "Блок щитом",
            ["warrior.shield_wall"] = "Стена щита",
            ["warrior.spell_reflection"] = "Отражение заклинаний",
            ["warrior.charge"] = "Рывок",
            ["warrior.intercept"] = "Перехват",
            ["warrior.disarm"] = "Разоружение",
            ["warrior.pummel"] = "Пинок",
        };

        public static string GetTitle(string id, string englishName)
            => _map.TryGetValue(id, out var ru) ? ru : englishName;
    }
}