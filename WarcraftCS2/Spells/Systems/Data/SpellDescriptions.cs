using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace WarcraftCS2.Spells.Systems.Data;
    /// <summary>
    /// Загружает и кэширует описания заклинаний из каталога
    /// {baseDir}/WarcraftCS2/Spells:
    ///  - общий Spells.txt (id=описание)
    ///  - отдельные файлы <id>.txt во всех подпапках
    ///
    /// Улучшения:
    ///  - Ключи id без учёта регистра
    ///  - Нормализация переносов строк
    ///  - Live-reload через FileSystemWatcher + debounce
    ///  - Удобные геттеры: TryGet / GetOrDefault / Set / Snapshot
    /// </summary>
    public static class SpellDescriptions
    {
        private static readonly ConcurrentDictionary<string, string> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        private static FileSystemWatcher? _watcher;
        private static string? _root;

        // Явно используем System.Timers.Timer (чтобы не конфликтовало с System.Threading.Timer)
        private static System.Timers.Timer? _debounce;

        /// <summary>
        /// Инициализация и первичная загрузка.
        /// baseDir — корень game (куда в релизе попадает папка WarcraftCS2).
        /// </summary>
        public static void LoadFromOutputTree(string baseDir)
        {
            _root = baseDir;
            Reload();
            TryEnableWatcher();
        }

        /// <summary>Получить описание, если есть.</summary>
        public static bool TryGet(string id, out string description)
            => _cache.TryGetValue(id, out description!);

        /// <summary>Описание или дефолтная фраза.</summary>
        public static string GetOrDefault(string id, string fallback = "Описание отсутствует")
            => _cache.TryGetValue(id, out var s) ? s : fallback;

        /// <summary>Принудительно задать/переопределить описание в рантайме.</summary>
        public static void Set(string id, string description)
            => _cache[id] = Normalize(description);

        /// <summary>Снимок текущего кэша для отладки/диагностики (копия).</summary>
        public static IReadOnlyDictionary<string, string> Snapshot()
            => new Dictionary<string, string>(_cache, _cache.Comparer);

        // ---------------- internal ----------------

        private static void Reload()
        {
            try
            {
                _cache.Clear();

                if (string.IsNullOrWhiteSpace(_root))
                    return;

                var folder = Path.Combine(_root!, "WarcraftCS2", "Spells");
                if (!Directory.Exists(folder))
                    return;

                // 1) общий файл Spells.txt (id=описание)
                var listFile = Path.Combine(folder, "Spells.txt");
                if (File.Exists(listFile))
                {
                    foreach (var raw in File.ReadAllLines(listFile))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//"))
                            continue;

                        var idx = line.IndexOf('=');
                        if (idx <= 0) continue;

                        var id = line[..idx].Trim();
                        var val = line[(idx + 1)..].Trim();
                        if (id.Length > 0)
                            _cache[id] = Normalize(val);
                    }
                }

                // 2) отдельные <id>.txt (во всех подпапках), Spells.txt пропускаем
                foreach (var path in Directory.EnumerateFiles(folder, "*.txt", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(path);
                    if (string.Equals(name, "Spells.txt", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var id = Path.GetFileNameWithoutExtension(path);
                    try
                    {
                        _cache[id] = Normalize(File.ReadAllText(path));
                    }
                    catch
                    {
                        // ignore read errors
                    }
                }
            }
            catch
            {
                // ignore reload errors, чтобы не ронять сервер
            }
        }

        private static void TryEnableWatcher()
        {
            try
            {
                var watchPath = Path.Combine(_root!, "WarcraftCS2", "Spells");
                if (!Directory.Exists(watchPath))
                    return;

                _watcher?.Dispose();
                _watcher = new FileSystemWatcher(watchPath)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };

                _watcher.Changed += OnFsChanged;
                _watcher.Created += OnFsChanged;
                _watcher.Deleted += OnFsChanged;
                _watcher.Renamed += OnFsChanged;
            }
            catch
            {
                // ignore watcher errors
            }
        }

        private static void OnFsChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                _debounce ??= new System.Timers.Timer(250) { AutoReset = false };
                _debounce.Stop();
                _debounce.Elapsed -= DebounceElapsed;
                _debounce.Elapsed += DebounceElapsed;
                _debounce.Start();
            }
            catch
            {
                // ignore debounce errors
            }
        }

        private static void DebounceElapsed(object? sender, System.Timers.ElapsedEventArgs e) => Reload();

        private static string Normalize(string s)
            => s.Replace("\r\n", "\n").Trim();
    }