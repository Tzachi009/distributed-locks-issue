using Medallion.Threading.Postgres;
using Serilog;
using System.Data;
using ILogger = Serilog.ILogger;

namespace DistributedLockIssueTestApp.Manangers
{
    internal class DistributedLockManager : IDistributedLockManager
    {
        private PostgresDistributedLock _postgresDistributedLock;
        private PostgresDistributedLockHandle _distributedLockHandle;
        private string _lockKeyName = string.Empty;
        private Guid _idForTesting;

        private readonly ILogger _logger = Log.ForContext<DistributedLockManager>();

        public async Task<bool> TryAcquire(string lockKeyName, IDbConnection dbConnection, TimeSpan timeout, CancellationToken token = default)
        {
            var distributedLock = CreateDistributedLock(lockKeyName, dbConnection);

            // _logger.Information($"Trying to acquire lock {_lockKeyName}...");

            _distributedLockHandle = await distributedLock.TryAcquireAsync(timeout, token);

            var isAcquired = _distributedLockHandle != null;

            if (isAcquired)
            {
                _idForTesting = Guid.NewGuid();

                _logger.Information($"Acquired the lock {_lockKeyName} successfully [ID: {_idForTesting}]");
            }
            else
            {
                _logger.Information($"Failed to acquire the lock {_lockKeyName}");
            }

            return isAcquired;
        }

        public void Dispose()
        {
            try
            {
                if (_distributedLockHandle != null)
                {
                    _distributedLockHandle.Dispose();

                    _distributedLockHandle = null;

                    _logger.Information($"Lock handle was disposed for lock {_lockKeyName} [ID: {_idForTesting}]");
                }
                else
                {
                    _logger.Information($"Lock handle was null while trying to dispose lock {_lockKeyName} [ID: {_idForTesting}]");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to dispose and release PostgreSql DB advisory lock {_lockKeyName} [ID: {_idForTesting}]");
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_distributedLockHandle != null)
                {
                    await _distributedLockHandle.DisposeAsync();

                    _distributedLockHandle = null;

                    _logger.Information($"Lock handle was disposed for lock {_lockKeyName} [ID: {_idForTesting}]");
                }
                else
                {
                    _logger.Information($"Lock handle was null while trying to dispose lock {_lockKeyName} [ID: {_idForTesting}]");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to dispose and release PostgreSql DB advisory lock {_lockKeyName} [ID: {_idForTesting}]");
            }
        }

        private PostgresDistributedLock CreateDistributedLock(string lockKeyName, IDbConnection dbConnection)
        {
            _lockKeyName = lockKeyName;

            var postgresAdvisoryLockKey = new PostgresAdvisoryLockKey(_lockKeyName);

            _postgresDistributedLock = new PostgresDistributedLock(postgresAdvisoryLockKey, dbConnection);

            return _postgresDistributedLock;
        }
    }
}

