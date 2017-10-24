using System.Collections.Generic;
using System.Linq;
using MoreLinq;

namespace NMetrics.Relations
{
    public static class RelationsInfoExtensions
    {
        public static IEnumerable<CompactedUsageInfo> Compact(this IEnumerable<Relation> usage)
        {
            var distinctTypesRelations = usage
             .Where(x => x.Target != null)
             .ToLookup(x => new { a = x.Source.FullName, b = x.Target.FullName })
             .Select(x => new CompactedUsageInfo()
             {
                 Source = x.Key.a,
                 Target = x.Key.b,
                 Usage = x.ToList()
             });
            return distinctTypesRelations;
        }
    }
}
