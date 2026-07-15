using IncentivePortal.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace IncentivePortal.Services;

public sealed class SqlServerDistributedLockService(IncentiveDbContext db) : IDistributedLockService
{
    public async Task<IAsyncDisposable> AcquireLockAsync(string resource, CancellationToken cancellationToken = default)
    {
        var resultParam = new SqlParameter
        {
            ParameterName = "@result",
            SqlDbType = SqlDbType.Int,
            Direction = ParameterDirection.Output
        };

        await db.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0",
            resultParam,
            new SqlParameter("@resource", resource),
            cancellationToken);

        if (resultParam.Value != DBNull.Value && (int)resultParam.Value < 0)
        {
            throw new InvalidOperationException($"Could not acquire lock for resource '{resource}'. It is currently locked by another process.");
        }

        return new SqlServerDistributedLock(db, resource);
    }

    private sealed class SqlServerDistributedLock(IncentiveDbContext db, string resource) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                var resultParam = new SqlParameter
                {
                    ParameterName = "@result",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.Output
                };

                await db.Database.ExecuteSqlRawAsync(
                    "EXEC @result = sp_releaseapplock @Resource = @resource, @LockOwner = 'Session'",
                    resultParam,
                    new SqlParameter("@resource", resource));
            }
            catch
            {
                // Swallow errors on release so we don't crash the teardown
            }
        }
    }
}
