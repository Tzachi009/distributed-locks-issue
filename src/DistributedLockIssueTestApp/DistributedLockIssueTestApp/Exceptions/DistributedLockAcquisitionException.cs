namespace DistributedLockIssueTestApp.Exceptions
{
    internal class DistributedLockAcquisitionException : Exception
    {
        public string DistributedLockName { get; }

        public DistributedLockAcquisitionException(string lockName) : base()
        {
            DistributedLockName = lockName;
        }

        public DistributedLockAcquisitionException(string lockName, string message) : base(message)
        {
            DistributedLockName = lockName;
        }
    }
}
