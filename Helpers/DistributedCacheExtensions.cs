using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace IncentivePortal.Helpers;

public static class DistributedCacheExtensions
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static async Task<T?> GetRecordAsync<T>(this IDistributedCache cache, string recordId, CancellationToken cancellationToken = default)
    {
        var jsonData = await cache.GetStringAsync(recordId, cancellationToken);
        if (jsonData is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(jsonData, Options);
    }

    public static async Task SetRecordAsync<T>(
        this IDistributedCache cache,
        string recordId,
        T data,
        TimeSpan? absoluteExpireTime = null,
        TimeSpan? unusedExpireTime = null,
        CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpireTime ?? TimeSpan.FromMinutes(60),
            SlidingExpiration = unusedExpireTime
        };

        var jsonData = JsonSerializer.Serialize(data, Options);
        await cache.SetStringAsync(recordId, jsonData, options, cancellationToken);
    }
}
