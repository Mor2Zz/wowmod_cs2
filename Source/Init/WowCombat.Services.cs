using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Timers;
// алиас для избежания конфликта с System.Threading.Timer
using CssTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

// явные using под системные неймспейсы в Spells/Systems
using WarcraftCS2.Spells.Systems.Config;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Resources;

namespace wowmod_cs2
{
    public partial class WowmodCs2
    {
        private CssTimer? _wow_resourceRegenTimer;

        public CombatConfig WowCombatConfig { get; private set; } = new();
        public CooldownsService WowCooldowns { get; private set; } = new();
        public GcdService WowGcd { get; private set; } = new();
        public ResourceService WowResources { get; private set; } = null!;

        // Инициализация боевых сервисов
        private void _wow_InitCombatServices()
        {
            WowResources = new ResourceService(WowCombatConfig);

            // самопере-планирующийся 1 Гц цикл
            void ScheduleRegen()
            {
                _wow_resourceRegenTimer = AddTimer(1.0f, () =>
                {
                    try { WowResources.TickRegen(); } catch { /* no-op */ }
                    ScheduleRegen();
                });
            }
            ScheduleRegen();

            Logger.LogInformation("[wowmod] Combat services initialized (GCD/CD/Mana)");
        }

        // Фабрика контекста для спеллов
        public CombatContext GetWowCombatContext()
            => new(WowResources, WowGcd, WowCooldowns, WowCombatConfig);
    }
}
