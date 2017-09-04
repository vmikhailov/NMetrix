using System.Collections.Generic;
using Mono.Cecil;

namespace NMetrics
{
    internal class TypeCacheInfo
    {
        public ISet<TypeDefinition> UsedTypes { get; set; }
        public bool Processing { get; set; }
    }
}