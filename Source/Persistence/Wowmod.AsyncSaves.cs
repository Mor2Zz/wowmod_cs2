using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WarcraftCS2.Gameplay;

namespace wowmod_cs2
{
    /// <summary>
    /// Partial with background, debounced async saving of _profiles.
    /// </summary>
    public partial class WowmodCs2
    {
        private readonly SemaphoreSlim _profilesSaveSem = new(1, 1);
        private int _profilesSavesQueued = 0;

        /// <summary>Queue a non-blocking save on a background thread.</summary>
        private void QueueProfilesSaveSnapshot()
        {
            try
            {
                // Take a snapshot under lock to avoid iterating while mutating
                Dictionary<ulong, PlayerProfile> snapshot;
                lock (_profilesLock) snapshot = new(_profiles);

                // Compute absolute path (same logic as SaveProfiles())
                var root = AppContext.BaseDirectory;
                var path = Path.Combine(root, _profilesJsonPath);

                Interlocked.Increment(ref _profilesSavesQueued);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _profilesSaveSem.WaitAsync().ConfigureAwait(false);
                        Interlocked.Decrement(ref _profilesSavesQueued);

                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

                        var tmp = path + ".tmp";
                        await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
                        File.Copy(tmp, path, overwrite: true);
                        File.Delete(tmp);
                    }
                    catch (Exception ex)
                    {
                        try { Logger.LogError(ex, "[wowmod] async profiles save failed"); } catch {}
                    }
                    finally
                    {
                        try { _profilesSaveSem.Release(); } catch {}
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[wowmod] queue save failed");
            }
        }
    }
}