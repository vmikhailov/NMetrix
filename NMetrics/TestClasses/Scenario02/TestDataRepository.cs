using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace NMetrics.TestClasses.Scenario02
{
    public class TestDataRepository : ITestDataRepository
    {
        public async Task<string> GetSingleValueAsync(int id)
        {
            using (var ctx = new TestDataContext())
            {
                return await ctx.TestData.Where(x => x.Id == id).Select(x => x.A).FirstOrDefaultAsync();
            }
        }

        public async Task<IQueryable<TestDataModel>> GetModelsAsync()
        {
            using (var ctx = new TestDataContext())
            {
                return (await ctx.TestData.ToListAsync()).AsQueryable();
            }
        }
    }
}