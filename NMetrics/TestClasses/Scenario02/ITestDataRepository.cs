using System.Linq;
using System.Threading.Tasks;

namespace NMetrics.TestClasses.Scenario02
{
    public interface ITestDataRepository
    {
        Task<string> GetSingleValueAsync(int id);
        Task<IQueryable<TestDataModel>> GetModelsAsync();
    }
}