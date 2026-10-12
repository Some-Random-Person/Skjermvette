using System.Collections.Concurrent;

namespace ScreenTime.Server.Services;

/// <summary>
/// Serialises budget changes per child, so two devices syncing at once can't both be granted
/// an offline allowance from the same remaining time. Only valid with a single server instance.
/// </summary>
public class ChildLocks
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<T> RunAsync<T>(Guid childId, Func<Task<T>> action, CancellationToken ct)
    {
        var semaphore = _locks.GetOrAdd(childId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
            return await action();
        }
        finally
        {
            semaphore.Release();
        }
    }
}
