using System.Linq;
using System.Threading.Tasks;

namespace NMetrics.TestClasses.Scenario02
{
    public interface ITestDataService 
    {
        string GetSingleValue(int id);
        IQueryable<TestDataModel> GetModels();

        Task<string> GetSingleValueAsync(int id);
        Task<IQueryable<TestDataModel>> GetModelsAsync();
    }

    //public interface IMyHttpController : IHttpController
    //{
        
    //}
}