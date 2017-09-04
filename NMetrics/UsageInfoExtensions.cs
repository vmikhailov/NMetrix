using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMetrics
{
    public static class UsageInfoExtensions
    {
        public static IEnumerable<CompactedUsageInfo> Compact(this IEnumerable<UsageInfo> usage)
        {
            var distinctTypesRelations = usage.Select(x => new
            {
                Source = x.UsingType.FullName,
                Target = x.UsedType.FullName,
                Original = x
            })
             .Where(x => x.Target != null)
             .GroupBy(x => new { x.Source, x.Target })
             .Select(x => new CompactedUsageInfo()
             {
                 Source = x.Key.Source,
                 Target = x.Key.Target,
                 Usage = x.Select(y => y.Original).ToList()
             })
             .ToList();
            return distinctTypesRelations;
        }
    }
}
