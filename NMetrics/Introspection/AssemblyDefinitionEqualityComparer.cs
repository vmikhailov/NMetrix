using System.Collections.Generic;
using Mono.Cecil;

namespace NMetrics.Introspection
{
    internal class AssemblyDefinitionEqualityComparer : IEqualityComparer<AssemblyDefinition>
    {
        public bool Equals(AssemblyDefinition x, AssemblyDefinition y)
        {
            if (x == null || y == null)
            {
                return false;
            }
            return x.FullName == y.FullName;
        }

        public int GetHashCode(AssemblyDefinition obj)
        {
            return obj.FullName.GetHashCode();
        }
    }
}