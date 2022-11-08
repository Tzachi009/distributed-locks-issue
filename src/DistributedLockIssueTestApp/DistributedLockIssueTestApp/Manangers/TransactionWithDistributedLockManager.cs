using DistributedLockIssueTestApp.EF;
using DistributedLockIssueTestApp.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Serilog;

namespace DistributedLockIssueTestApp.Manangers
{
    internal class TransactionWithDistributedLockManager : ITransactionWithDistributedLockManager
    {
        private IDistributedLockManager _distributedLockManager;
        private MyDataContext _dataContext = null;
        private IDbContextTransaction _transaction = null;

        private readonly ILogger _logger = Log.ForContext<TransactionWithDistributedLockManager>();

        public TransactionWithDistributedLockManager(IDistributedLockManager distributedLockManager, IDbContextFactory<MyDataContext> dataContextFactory)
        {
            _distributedLockManager = distributedLockManager;

            _dataContext = dataContextFactory.CreateDbContext();
        }

        public async Task Initialize(string distributedLockKey, TimeSpan? lockRetrievalTimeout = null, int lockRetrievalRetriesCount = 0, TimeSpan? lockRetrievalSleepTimeBetweenRetries = null, CancellationToken token = default)
        {
            try
            {
                var isLockAcquired = false;

                if (lockRetrievalTimeout == null)
                {
                    lockRetrievalTimeout = TimeSpan.Zero;
                }

                if (lockRetrievalSleepTimeBetweenRetries == null)
                {
                    lockRetrievalSleepTimeBetweenRetries = TimeSpan.Zero;
                }

                var dbConnection = _dataContext.Database.GetDbConnection();

                // If we do not open the connection, DistributedLock library will throw an exception because the connection is externally owned
                await dbConnection.OpenAsync(token);

                for (int retryAttempt = 0; (!isLockAcquired) && retryAttempt <= lockRetrievalRetriesCount; retryAttempt++)
                {
                    isLockAcquired = await _distributedLockManager.TryAcquire(distributedLockKey, dbConnection, lockRetrievalTimeout.Value, token);

                    if (!isLockAcquired)
                    {
                        await Task.Delay(lockRetrievalSleepTimeBetweenRetries.Value, token);
                    }
                }

                // If the lock was not acquired up until now, throw an exception
                if (!isLockAcquired)
                {
                    throw new DistributedLockAcquisitionException(distributedLockKey);
                }

                _transaction = await _dataContext.Database.BeginTransactionAsync(token);
            }
            catch (DistributedLockAcquisitionException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task Commit(CancellationToken token = default)
        {
            try
            {
                // We create and invoke a commit command internally, instead of using Npgsql API, since it will close the connection and the lock will never be released correctly 
                await CommitInternal(token);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while trying to commit a transaction");

                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                _distributedLockManager?.Dispose();
                _distributedLockManager = null;

                _transaction?.Dispose();
                _transaction = null;

                _dataContext?.Dispose();
                _dataContext = null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while trying to dispose a distributed lock, transaction and context");
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_distributedLockManager != null)
                {
                    await _distributedLockManager.DisposeAsync();

                    _distributedLockManager = null;
                }

                if (_transaction != null)
                {
                    await _transaction.DisposeAsync();

                    _transaction = null;
                }

                if (_dataContext != null)
                {
                    await _dataContext.DisposeAsync();

                    _dataContext = null;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while trying to dispose a distributed lock, transaction and context");
            }
        }

        private async Task CommitInternal(CancellationToken token = default)
        {
            var transaction = _transaction.GetDbTransaction() as NpgsqlTransaction;
            var dbConnection = _dataContext.Database.GetDbConnection() as NpgsqlConnection;

            await using (var cmd = new NpgsqlCommand("COMMIT", dbConnection, transaction))
            {
                var reader = await cmd.ExecuteNonQueryAsync(token);
            }
        }
    }
}
