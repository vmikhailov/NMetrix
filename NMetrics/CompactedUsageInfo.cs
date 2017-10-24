using System.Collections.Generic;
using System.Diagnostics;
using NMetrics.Relations;

namespace NMetrics
{
    [DebuggerDisplay("[{Source}] -> [{Target}] with {UsageCount}")]
    public class CompactedUsageInfo
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public List<Relation> Usage { get; set; }
        public int UsageCount => Usage?.Count ?? 0;
    }
}