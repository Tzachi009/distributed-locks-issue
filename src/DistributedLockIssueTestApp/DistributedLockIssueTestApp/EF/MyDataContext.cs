using Microsoft.EntityFrameworkCore;

namespace DistributedLockIssueTestApp.EF
{
    internal class MyDataContext : DbContext
    {
        public MyDataContext(DbContextOptions<MyDataContext> options) : base(options)
        {
        }
    }
}
