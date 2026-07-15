namespace IncentivePortal.Services;

public interface IDistributedLockService
{
    /// <summary>
    /// Acquires a distributed lock on the specified resource.
    /// Returns an IAsyncDisposable that releases the lock when disposed.
    /// If the lock cannot be acquired (e.g. timeout or already locked by someone else), throws an InvalidOperationException.
    /// </summary>
    Task<IAsyncDisposable> AcquireLockAsync(string resource, CancellationToken cancellationToken = default);
}
