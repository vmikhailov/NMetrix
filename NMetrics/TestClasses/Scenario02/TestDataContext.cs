using System.Data.Entity;

namespace NMetrics.TestClasses.Scenario02
{
    public class TestDataContext: DbContext
    {
        public DbSet<TestDataModel> TestData { get; set; }
    }
}