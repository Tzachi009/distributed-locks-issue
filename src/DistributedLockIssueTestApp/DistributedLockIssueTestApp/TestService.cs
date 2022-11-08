using DistributedLockIssueTestApp.EF;
using DistributedLockIssueTestApp.Exceptions;
using DistributedLockIssueTestApp.Manangers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;
using ILogger = Serilog.ILogger;

namespace DistributedLockIssueTestApp
{
    internal class TestService : BackgroundService
    {
        // Change the number of threads to 8 or more to reproduce the issue reliably. With 4 threads or less the issue doesn't reproduce reliably.
        private readonly int _numberOfThreads = 8;
        // The distributed lock key name. Should be the same for all threads. 
        private readonly string _distributedLockKeyName = "1";
        // The timeout for acquiring the distributed lock.
        private readonly TimeSpan _lockAcquisitionTimeout = TimeSpan.FromMilliseconds(200);
        // The number of times to keep retrying to acquire the lock. This is intentionally a big number, in order to let a thread a very fair chance to hold the lock without throwing an exception.
        private readonly int _numberOfLockAcquisitionRetries = 1000;
        // The time to wait between lock acquisition retries.
        private readonly TimeSpan _timeToWaitBetweenRetries = TimeSpan.FromMilliseconds(20);

        private readonly IDbContextFactory<MyDataContext> _dataContextFactory;
        private readonly ILogger _logger = Log.ForContext<TestService>();

        public TestService(IDbContextFactory<MyDataContext> dataContextFactory)
        {
            _dataContextFactory = dataContextFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            for (int i = 0; i < _numberOfThreads; i++)
            {
                await Task.Factory.StartNew(TryHoldLock);
            }
        }

        private async Task TryHoldLock()
        {
            _logger.Information("Starting TryHoldLock loop...");

            while (true)
            {
                try
                {
                    await using (var transactionWithDistributedLockMananger = await CreateTransactionWithDistributedLockMananger())
                    {
                        // We wait for a random number of a few milliseconds (up to 50 MS)
                        await Task.Delay(Random.Shared.Next(20, 51));

                        await transactionWithDistributedLockMananger.Commit();
                    } // Lock is disposed and released
                }
                catch (DistributedLockAcquisitionException ex)
                {
                    _logger.Error($"Failed to hold the lock {ex.DistributedLockName}");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error occurred in the TryHoldLock loop");
                }
            }
        }

        private async Task<ITransactionWithDistributedLockManager> CreateTransactionWithDistributedLockMananger()
        {
            // We create the managers without using DI in order to dispose them correctly, by disallowing .Net's built-in container to hold them
            // https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines#disposable-transient-services-captured-by-container
            // https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines#idisposable-guidance-for-transient-and-shared-instances
            var distributedLockMananger = new DistributedLockManager();
            var transactionWithDistributedLockMananger = new TransactionWithDistributedLockManager(distributedLockMananger, _dataContextFactory);

            // The actual method that tries to hold the lock and start a transaction
            await transactionWithDistributedLockMananger.Initialize(_distributedLockKeyName, _lockAcquisitionTimeout, _numberOfLockAcquisitionRetries, _timeToWaitBetweenRetries);

            return transactionWithDistributedLockMananger;
        }
    }
}
