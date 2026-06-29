using System;
using System.Threading;

namespace ORTools.Worker;

/// <summary>
/// Thread-safe, lock-free map reading cache.
/// Prevents multiple macro threads from redundantly reading map name from memory within the same second.
/// </summary>
public sealed class MapCache
{
    private volatile string _cachedMap = string.Empty;
    private long _mapCacheTicks = 0;
    private const long MAP_CACHE_TICKS = 10_000_000; // 1 second

    public string GetCachedMap(Func<string> mapReadFunc)
    {
        long now = DateTime.UtcNow.Ticks;
        if (now - Interlocked.Read(ref _mapCacheTicks) > MAP_CACHE_TICKS)
        {
            _cachedMap = mapReadFunc() ?? string.Empty;
            Interlocked.Exchange(ref _mapCacheTicks, now);
        }
        return _cachedMap;
    }
}
