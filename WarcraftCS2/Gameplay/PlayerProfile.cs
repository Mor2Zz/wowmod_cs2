using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WarcraftCS2.Gameplay
{
    public class PlayerProfile
    {
        public ulong Key { get; set; }
        public string Name { get; set; } = "Unknown";

        public int Level { get; set; } = 1;
        public int Exp { get; set; } = 0;
        public int TalentPoints { get; set; } = 0;

        // Текущий класс
        public string ClassId { get; set; } = "";

        // Старые бинды 1..4 (оставлены для обратной совместимости)
        public Dictionary<int, string> Binds { get; set; } = new();

        // НОВОЕ: назначение Ability / Ultimate в меню Spells
        public string? Ability  { get; set; } = null;
        public string? Ultimate { get; set; } = null;

        // ВАЖНО: таланты игрока — нужен Registry/TalentsMenu
        public HashSet<string> Talents { get; set; } = new();

        // --- XP helpers (как было) ---
        [JsonIgnore]
        public int BaseExpToNext { get; set; } = 100;

        [JsonIgnore]
        public int ExpPerLevelAdd { get; set; } = 25;

        public bool AddExp(int amount, int baseToNext, int perLevelAdd)
        {
            Exp += amount;
            var leveled = false;
            while (Exp >= ExpToNext(baseToNext, perLevelAdd))
            {
                var need = ExpToNext(baseToNext, perLevelAdd);
                Exp -= need;
                Level++;
                TalentPoints++;
                leveled = true;
            }
            return leveled;
        }

        public int ExpToNext(int baseToNext, int perLevelAdd)
            => baseToNext + (Level - 1) * perLevelAdd;
    }
}