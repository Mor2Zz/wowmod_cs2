using System;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Хелперы для прок-условий: "в спину", по углу, по высоте, крит-флаги (пробросом).
    public static class ProcHelpers
    {
        static Vector3 Normalize(Vector3 v)
        {
            float len = v.Length();
            return len > 1e-6f ? v / len : Vector3.Zero;
        }

        // угол между forward и направлением "из from в to" в градусах
        public static float AngleDeg(Vector3 fromPos, Vector3 forward, Vector3 toPos)
        {
            var dir = Normalize(toPos - fromPos);
            var fwd = Normalize(forward);
            float dot = Vector3.Dot(fwd, dir);
            dot = MathF.Max(-1f, MathF.Min(1f, dot));
            return MathF.Acos(dot) * (180f / MathF.PI);
        }

        // "в спину" для target: attacker находится в секторе позади targetForward (>= backHalfAngleDeg от лицевой).
        public static bool IsBehind(Vector3 attackerPos, Vector3 targetPos, Vector3 targetForward, float backHalfAngleDeg = 60f)
        {
            var dirTtoA = Normalize(attackerPos - targetPos);
            var fwdT    = Normalize(targetForward);
            // сзади => угловое отклонение от "против forward" меньше половинного
            float dot = Vector3.Dot(fwdT * -1f, dirTtoA);
            float cos = MathF.Cos(backHalfAngleDeg * MathF.PI / 180f);
            return dot >= cos;
        }

        public static bool HeightWithin(Vector3 a, Vector3 b, float maxAbsDelta)
            => MathF.Abs(a.Z - b.Z) <= MathF.Abs(maxAbsDelta);

        // Обёртки со снапшотами (если forward уже известен, передавай его сюда).
        public static bool IsBehind(TargetSnapshot attacker, TargetSnapshot target, Vector3 targetForward, float backHalfAngleDeg = 60f)
            => IsBehind(attacker.Position, target.Position, targetForward, backHalfAngleDeg);

        public static bool ConeHit(TargetSnapshot caster, Vector3 casterForward, TargetSnapshot target, float halfAngleDeg)
            => AngleDeg(caster.Position, casterForward, target.Position) <= halfAngleDeg;
    }
}