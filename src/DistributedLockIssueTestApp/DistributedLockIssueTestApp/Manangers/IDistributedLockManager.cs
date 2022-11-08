using System.Data;

namespace DistributedLockIssueTestApp.Manangers
{
    internal interface IDistributedLockManager : IDisposable, IAsyncDisposable
    {
        public Task<bool> TryAcquire(string lockKeyName, IDbConnection dbConnection, TimeSpan timeout, CancellationToken token = default);
    }
}
