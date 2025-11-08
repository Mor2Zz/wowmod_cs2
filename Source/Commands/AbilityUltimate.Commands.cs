using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;

namespace wowmod_cs2
{
    public partial class WowmodCs2 : BasePlugin
    {
        [ConsoleCommand("ultimate")]
        [ConsoleCommand("wow_ultimate")]
        public void CmdUltimate(CCSPlayerController player, CommandInfo info)
        {
            if (player is null || !player.IsValid) return;
            CastUltimate(player);
        }

        [ConsoleCommand("ability")]
        [ConsoleCommand("wow_ability")]
        public void CmdAbility(CCSPlayerController player, CommandInfo info)
        {
            if (player is null || !player.IsValid) return;
            CastAbility(player);
        }

        private void CastAbility(CCSPlayerController player)
        {
            var prof = GetOrCreateProfile(player);
            if (string.IsNullOrWhiteSpace(prof.Ability))
            { player.PrintToChat("[wowmod] Ability не назначена."); return; }
            CastSpell(player, prof.Ability);
        }

        private void CastUltimate(CCSPlayerController player)
        {
            var prof = GetOrCreateProfile(player);
            if (string.IsNullOrWhiteSpace(prof.Ultimate))
            { player.PrintToChat("[wowmod] Ultimate не назначена."); return; }
            CastSpell(player, prof.Ultimate);
        }
    }
}