using System.Linq;
using System.Threading.Tasks;

namespace NMetrics.TestClasses.Scenario02
{
    public class DefaultTestDataService : ITestDataService
    {
        IConfigurationService configuration;
        ITestDataRepository repository;

        public DefaultTestDataService(IConfigurationService configuration, ITestDataRepository repository)
        {
            this.configuration = configuration;
            this.repository = repository;
        }
        public string GetSingleValue(int id)
        {
            return GetSingleValueAsync(id).Result;
        }

        public IQueryable<TestDataModel> GetModels()
        {
            return GetModelsAsync().Result;
        }

        public async Task<string> GetSingleValueAsync(int id)
        {
            return await repository.GetSingleValueAsync(id);
        }

        public async Task<IQueryable<TestDataModel>> GetModelsAsync()
        {
            return await repository.GetModelsAsync();
        }
    }
}