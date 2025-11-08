namespace WarcraftCS2.Spells.Systems.Damage
{
    public enum DamageSchool
    {
        Physical = 0,
        Fire,
        Frost,
        Arcane,
        Holy,
        Shadow,
        Nature,
        Poison
    }

    [Flags]
    public enum DamageSchoolMask
    {
        None     = 0,
        Physical = 1 << 0,
        Fire     = 1 << 1,
        Frost    = 1 << 2,
        Arcane   = 1 << 3,
        Holy     = 1 << 4,
        Shadow   = 1 << 5,
        Nature   = 1 << 6,
        Poison   = 1 << 7,
        All      = ~0
    }

    public static class DamageSchoolExt
    {
        public static DamageSchoolMask ToMask(this DamageSchool s) =>
            s switch
            {
                DamageSchool.Physical => DamageSchoolMask.Physical,
                DamageSchool.Fire     => DamageSchoolMask.Fire,
                DamageSchool.Frost    => DamageSchoolMask.Frost,
                DamageSchool.Arcane   => DamageSchoolMask.Arcane,
                DamageSchool.Holy     => DamageSchoolMask.Holy,
                DamageSchool.Shadow   => DamageSchoolMask.Shadow,
                DamageSchool.Nature   => DamageSchoolMask.Nature,
                DamageSchool.Poison   => DamageSchoolMask.Poison,
                _ => DamageSchoolMask.None
            };
    }
}