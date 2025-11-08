using System;

namespace wowmod_cs2.Config
{
    public record WowConfig
    {
        public XpSection Xp { get; init; } = new();
        public StorageSection Storage { get; init; } = new();

        public static WowConfig Default() => new();

        public record XpSection
        {
            public int BaseKill { get; init; } = 50;
            public int HeadshotBonus { get; init; } = 25;

            public int Plant { get; init; } = 100;
            public int Defuse { get; init; } = 100;
            public int Explode { get; init; } = 75;
            public int Pickup { get; init; } = 15;
            public int Drop { get; init; } = 15;
            public int AbortPlant { get; init; } = 25;

            public int BaseToNext { get; init; } = 250;
            public int PerLevelAdd { get; init; } = 50;
        }

        public record StorageSection
        {
            // путь где храним профили
            public string? Path { get; init; } = "addons/counterstrikesharp/configs/plugins/wowmod-cs2/players.json";
        }
    }
}
