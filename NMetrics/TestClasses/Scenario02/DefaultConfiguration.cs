namespace NMetrics.TestClasses.Scenario02
{
    public class DefaultConfiguration : IConfigurationService
    {
        public string GetConfigValue(string name)
        {
            return $"Value = {name}";
        }
    }
}