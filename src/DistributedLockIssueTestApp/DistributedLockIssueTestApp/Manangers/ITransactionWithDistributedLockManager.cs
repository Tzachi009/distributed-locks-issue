namespace DistributedLockIssueTestApp.Manangers
{
    internal interface ITransactionWithDistributedLockManager : IDisposable, IAsyncDisposable
    {
        public Task Initialize(string distributedLockKey, TimeSpan? lockRetrievalTimeout = null, int lockRetrievalRetriesCount = 0,
            TimeSpan? lockRetrievalSleepTimeBetweenRetries = null, CancellationToken token = default);

        public Task Commit(CancellationToken token = default);
    }
}
