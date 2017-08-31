using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;

namespace NMetrics
{
    public static class AssemblyListExtension
    {
        public static IEnumerable<AssemblyDefinition> DedupFiles(this IEnumerable<AssemblyDefinition> files)
        {
            var dict = files
                .Select(x => new { name = x.FullName.Split('/').Last(), def = x })
                .ToLookup(x => x.name, x => x.def)
                .ToDictionary(x => x.Key, x => x.First());

            return dict.Values.ToList();
        }
    }
}
