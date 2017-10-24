namespace NMetrics.TestClasses.Scenario02
{
    public interface IExtendedDataService : ITestDataService
    {
        void SetSingleValue(int id, string value);
    }
}