using System.Collections.Generic;

namespace NMetrics
{
    public class CompactedUsageInfo
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public List<UsageInfo> Usage { get; set; }
    }
}