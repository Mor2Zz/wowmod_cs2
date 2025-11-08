using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Timers;
using CssTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace wowmod_cs2
{
    public partial class WowmodCs2
    {
        private bool _wow_bootstrapDone;
        private CssTimer? _wow_autosaveTimer;

        private void EnsureBootstrap()
        {
            if (_wow_bootstrapDone) return;

            LoadProfilesSafe();

            _wow_InitCombatServices();   // мана/GCD/КД
            _wow_InitDamageServices();   // иммунитеты/щиты/пайплайн

            // автосейв
            void ScheduleAutosave()
            {
                _wow_autosaveTimer = AddTimer(60.0f, () =>
                {
                    try { SaveProfilesSafe(); } catch { /* no-op */ }
                    ScheduleAutosave();
                });
            }
            ScheduleAutosave();

            _wow_bootstrapDone = true;
            Logger.LogInformation("[wowmod] bootstrap complete (profiles + combat + damage + autosave)");
        }
    }
}