using System;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems
{
    /// Тонкий рантайм-контракт для паттернов спеллов.
    public interface ISpellRuntime
    {
        // Время/случайность
        float Now();
        Random Rng(); 

        // Идентификация
        int SidOf(TargetSnapshot snap);

        // Базовые проверки
        bool IsAlive(TargetSnapshot snap);
        bool IsEnemy(TargetSnapshot caster, TargetSnapshot target);
        bool IsAlly (TargetSnapshot caster, TargetSnapshot target);

        // Иммунитеты/резисты
        bool HasImmunity(int sid, string tagOrSchool); // e.g. "magic", "physical", "holy", "stun", "poly"
        float GetResist01(int sid, string school);     // 0..1

        // Ресурсы/гейты 
        bool HasMana(int sid, float cost);
        void ConsumeMana(int sid, float cost);
        void StartGcd (int sid, float gcdSeconds);
        void StartCooldown(int sid, int spellId, float cdSeconds);

        // Урон/хил/щит/ауры
        void DealDamage(int srcSid, int tgtSid, int spellId, float amount, string school);
        void Heal      (int srcSid, int tgtSid, int spellId, float amount);
        void ApplyShield(int srcSid, int tgtSid, int spellId, string tag, float capacity, float duration);
        void ApplyAura  (int srcSid, int tgtSid, int spellId, string tag, float magnitude, float duration);
        void RemoveAuraByTag(int tgtSid, string tag);

        // Контроль/диспел
        float ApplyControlWithDr(int srcSid, int tgtSid, int spellId, string controlTag, float baseDuration); // вернет фактическую длительность после DR
        int DispelByCategory(int tgtSid, string category, int maxCount); // "magic", "curse", "poison", "disease", ...

        // Периодики/каналы
        IPeriodicHandle StartPeriodic(int srcSid, int tgtSid, int spellId, float duration, float tickEvery, Action onTick, Action onEnd);
        IChannelHandle  StartChannel (int srcSid, int spellId, float duration, float tickEvery, Func<bool> isCancelled, Action onTick, Action onEnd);

        // Движение (опционально)
        bool TryBlink(int sid, Vector3 worldOffsetOrAbsTarget, bool isOffset);

        // Визуал/звук (заглушки под твой VisualService)
        void Fx(string effectId, TargetSnapshot at);
        void Sfx(string soundId, TargetSnapshot at);
    }

    public interface IPeriodicHandle { void Stop(); }
    public interface IChannelHandle  { void Stop(); }
}