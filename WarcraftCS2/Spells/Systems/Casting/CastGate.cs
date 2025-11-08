namespace WarcraftCS2.Spells.Systems.Casting
{
    // Унифицированная «калитка» начала каста
    public static class CastGate
    {
        public static bool TryBeginCast(
            CombatContext ctx,
            ulong casterSid,
            string spellId,
            double manaCost,
            double cooldownSec,
            out string failReason,
            bool consumeResources = true)
        {
            if (ctx.Gcd.IsOnGcd(casterSid, out var gcdRemain))
            {
                failReason = $"GCD {gcdRemain:0.00}s";
                return false;
            }

            if (!ctx.Cooldowns.IsReady(casterSid, spellId, out var cdRemain))
            {
                failReason = $"КД {cdRemain:0.0}s";
                return false;
            }

            if (consumeResources && !ctx.Resources.TryConsume(casterSid, manaCost))
            {
                var rs = ctx.Resources.Get(casterSid);
                failReason = $"Недостаточно маны ({rs.Mana:0}/{manaCost:0})";
                return false;
            }

            ctx.Gcd.Start(casterSid, ctx.Config.GlobalGcdSec);
            if (cooldownSec > 0) ctx.Cooldowns.Start(casterSid, spellId, cooldownSec);
            ctx.Resources.MarkCast(casterSid);

            failReason = "";
            return true;
        }
    }

    // Сшивает боевые сервисы
    public sealed class CombatContext
    {
        public Resources.ResourceService Resources { get; }
        public GcdService Gcd { get; }
        public CooldownsService Cooldowns { get; }
        public Config.CombatConfig Config { get; }

        public CombatContext(Resources.ResourceService res, GcdService gcd, CooldownsService cd, Config.CombatConfig cfg)
        {
            Resources = res;
            Gcd = gcd;
            Cooldowns = cd;
            Config = cfg;
        }
    }
}