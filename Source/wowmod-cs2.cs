using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Status;
using WarcraftCS2.Spells.Systems.Data;
using wowmod_cs2.Config;
using wowmod_cs2.Persistence;
using RPG.XP;               

namespace wowmod_cs2
{
    [MinimumApiVersion(80)]
    public partial class WowmodCs2 : BasePlugin, IWowRuntime
    {
        public override string ModuleName => "wowmod-cs2";
        public override string ModuleVersion => "0.6.0";
        public override string ModuleAuthor => "Mor2Z";

        private readonly object _profilesLock = new();
        private readonly Dictionary<ulong, PlayerProfile> _profiles = new();

        private WowConfig _cfg = WowConfig.Default();
        private IStorage _storage = new JsonStorage();
        private string _profilesJsonPath = "addons/counterstrikesharp/configs/plugins/wowmod-cs2/players.json";
        private readonly Random _rng = new();

        // IWowRuntime
        public WowConfig Config => _cfg;
        public Random Rng => _rng;
        public PlayerProfile GetProfile(CCSPlayerController player) => GetOrCreateProfile(player);
        public void Print(CCSPlayerController player, string msg) => player.PrintToChat(msg);
        public void Save() => SaveProfiles();

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            LoadConfig();

            WowRegistry.RegisterAllFromAssembly(Assembly.GetExecutingAssembly());
            SpellDescriptions.LoadFromOutputTree(AppContext.BaseDirectory);

            XpScaler.InitDefaults();

            InitWowMenu();

            Logger.LogInformation("[wowmod] config loaded: kill={kill}, hs+{hs}",
                _cfg.Xp.BaseKill, _cfg.Xp.HeadshotBonus);
        }

        public override void Load(bool hotReload)
        {
            // инфо/сервис
            AddCommand("css_wowstats", "Show stats", CmdStats);
            AddCommand("css_wowreset", "Reset class", CmdReset);
            AddCommand("css_wowsave", "Force save", CmdSave);
            AddCommand("wow", "Open WOW menu", CmdWow);

            // каст активки по id
            AddCommand("wow_cast", "Cast spell by id", (p, info) =>
            {
                if (p is null || !p.IsValid) return;
                if (info.ArgCount < 2) { p.PrintToChat("[wowmod] Использование: wow_cast <spellId>"); return; }
                var id = info.GetArg(1).ToLowerInvariant();
                CastSpell(p, id);
            });

            // бинды
            AddCommand("wow_bind", "Bind active spell to slot 1..4", (p, info) =>
            {
                if (p is null || !p.IsValid) return;
                if (info.ArgCount < 3) { p.PrintToChat("[wowmod] Использование: wow_bind <1..4> <spellId>"); return; }
                if (!int.TryParse(info.GetArg(1), out var slot) || slot is < 1 or > 4)
                { p.PrintToChat("[wowmod] Слот должен быть 1..4"); return; }

                var id = info.GetArg(2).ToLowerInvariant();
                var prof = GetOrCreateProfile(p);

                if (!WowRegistry.Classes.TryGetValue(prof.ClassId, out var cls))
                { p.PrintToChat("[wowmod] Сначала выбери класс."); return; }

                if (!cls.ActiveSpells.Contains(id))
                { p.PrintToChat("[wowmod] Этот спелл не относится к твоему классу."); return; }

                prof.Binds[slot] = id;
                SaveProfiles();
                p.PrintToChat($"[wowmod] Привязал слот {slot} к {id}.");
            });

            AddCommand("wow_castslot", "Cast quick slot 1..4", (p, info) =>
            {
                if (p is null || !p.IsValid) return;
                if (info.ArgCount < 2 || !int.TryParse(info.GetArg(1), out var slot) || slot is < 1 or > 4)
                { p.PrintToChat("[wowmod] Использование: wow_castslot <1..4>"); return; }
                CastBind(p, slot);
            });

            // быстрые алиасы: wow_1..wow_4
            AddCommand("wow_1", "Cast slot 1", (p, _) => { if (p is { IsValid: true }) CastBind(p, 1); });
            AddCommand("wow_2", "Cast slot 2", (p, _) => { if (p is { IsValid: true }) CastBind(p, 2); });
            AddCommand("wow_3", "Cast slot 3", (p, _) => { if (p is { IsValid: true }) CastBind(p, 3); });
            AddCommand("wow_4", "Cast slot 4", (p, _) => { if (p is { IsValid: true }) CastBind(p, 4); });

            // ивенты прогресса/бомбы/урона/спавна
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath, HookMode.Post);

            // XP за бомбу НЕ начисляем (хуки оставлены для геймплейной логики, если нужно)
            RegisterEventHandler<EventBombBeginplant>(OnBombBeginplant, HookMode.Post);
            RegisterEventHandler<EventBombAbortplant>(OnBombAbortplant, HookMode.Post);
            RegisterEventHandler<EventBombPlanted>(OnBombPlanted, HookMode.Post);
            RegisterEventHandler<EventBombDefused>(OnBombDefused, HookMode.Post);
            RegisterEventHandler<EventBombExploded>(OnBombExploded, HookMode.Post);
            RegisterEventHandler<EventBombPickup>(OnBombPickup, HookMode.Post);
            RegisterEventHandler<EventBombDropped>(OnBombDropped, HookMode.Post);

            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawned, HookMode.Post);
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt, HookMode.Post);

            // <<< ВАЖНО: регистрируем тикер эффектов >>>
            EffectsTicker.Register(this);

            Logger.LogInformation("[wowmod] plugin loaded");
        }

        public override void Unload(bool hotReload)
        {
            // <<< ВАЖНО: отписываем тикер эффектов >>>
            EffectsTicker.Unregister(this);

            SaveProfiles();
            Logger.LogInformation("[wowmod] plugin unloaded (saved profiles)");
        }

        // ====== каст по id ======
        private void CastSpell(CCSPlayerController player, string spellId)
        {
            var prof = GetOrCreateProfile(player);

            if (!WowRegistry.Classes.TryGetValue(prof.ClassId, out var cls))
            { player.PrintToChat("[wowmod] Класс не выбран."); return; }

            if (!cls.ActiveSpells.Contains(spellId))
            { player.PrintToChat("[wowmod] Этот спелл не относится к твоему классу."); return; }

            if (!WowRegistry.Spells.TryGetValue(spellId, out var sp))
            { player.PrintToChat("[wowmod] Спелл не зарегистрирован."); return; }

            sp.OnCast(this, player);
        }

        // ====== каст связанного слота ======
        private void CastBind(CCSPlayerController player, int slot)
        {
            var prof = GetOrCreateProfile(player);
            if (!prof.Binds.TryGetValue(slot, out var id) || string.IsNullOrWhiteSpace(id))
            { player.PrintToChat($"[wowmod] Слот {slot} не привязан. Используй: wow_bind {slot} <spellId>"); return; }

            CastSpell(player, id);
        }

        // ====== команды ======
        private void CmdStats(CCSPlayerController? player, CommandInfo _)
        {
            if (player is null || !player.IsValid) return;
            var p = GetOrCreateProfile(player);
            WowRegistry.Classes.TryGetValue(p.ClassId, out var cls);
            player.PrintToChat($"[wowmod] {p.Name}: LVL {p.Level}, XP {p.Exp}/{p.ExpToNext(_cfg.Xp.BaseToNext,_cfg.Xp.PerLevelAdd)}, CLASS {(cls?.Name ?? "None")}, TP {p.TalentPoints}");
        }

        private void CmdReset(CCSPlayerController? player, CommandInfo _)
        {
            if (player is null || !player.IsValid) return;
            var p = GetOrCreateProfile(player);
            p.ClassId = "";
            p.Binds.Clear();
            player.PrintToChat("[wowmod] Класс сброшен.");
            SaveProfiles();
        }

        private void CmdSave(CCSPlayerController? player, CommandInfo _)
        {
            SaveProfiles();
            player?.PrintToChat("[wowmod] Сохранение выполнено.");
        }

        // ====== профили ======
        private PlayerProfile GetOrCreateProfile(CCSPlayerController player)
        {
            ulong key;
            try { key = player.SteamID; } catch { key = 0; }
            if (key == 0) key = 9_000_000_000_000_000_000UL + (ulong)player.Slot;

            lock (_profilesLock)
            {
                if (!_profiles.TryGetValue(key, out var prof))
                {
                    prof = new PlayerProfile { Key = key, Name = player.PlayerName ?? "Unknown" };
                    _profiles[key] = prof;
                }
                return prof;
            }
        }

        private void LoadConfig()
        {
            try
            {
                var root = AppContext.BaseDirectory;
                var cfgPath = Path.Combine(root, "addons", "counterstrikesharp", "configs", "wow", "wow.json");
                _cfg = WowConfigLoader.LoadOrDefault(cfgPath);
                _profilesJsonPath = _cfg.Storage.Path ?? _profilesJsonPath;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[wowmod] config load failed; using defaults");
                _cfg = WowConfig.Default();
            }
        }

        private void SaveProfiles()
        {
            try
            {
                var root = AppContext.BaseDirectory;
                var path = Path.Combine(root, _profilesJsonPath);
                _ = _storage.SaveAsync(path, _profiles);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[wowmod] save profiles failed");
            }
        }

        // ====== Добавление опыта + уведомления ======
        private void AddXpAndNotify(CCSPlayerController player, int amount, string? reason = null)
        {
            if (player is null || !player.IsValid) return;

            var prof = GetOrCreateProfile(player);
            var leveled = prof.AddExp(amount, _cfg.Xp.BaseToNext, _cfg.Xp.PerLevelAdd);

            var need = prof.ExpToNext(_cfg.Xp.BaseToNext, _cfg.Xp.PerLevelAdd);
            var suffix = string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason})";

            player.PrintToChat($"[wowmod] +{amount} XP{suffix}. LVL {prof.Level}: {prof.Exp}/{need}.");
            if (leveled)
                player.PrintToChat("[wowmod] Поздравляем! Новый уровень! (+1 Talent Point)");

            // при желании можно сразу сохранять:
            // SaveProfiles();
        }

        // ← NEW: унифицированный метод, который вызывают наши event-хэндлеры XP
        private void GiveXp(CCSPlayerController player, int amount, string reason = "")
            => AddXpAndNotify(player, amount, reason);
    }
}