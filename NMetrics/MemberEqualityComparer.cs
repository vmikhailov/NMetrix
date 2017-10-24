using System.Collections.Generic;
using Mono.Cecil;

namespace NMetrics
{
    public class TypeReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : TypeReference
    {
        public bool Equals(T x, T y)
        {
            if (x == null || y == null)
            {
                return false;
            }
            if (x.FullName != y.FullName)
            {
                return false;
            }
            if (GetScopeName(x.Scope) != GetScopeName(y.Scope))
            {
                return false;
            }
            return true;
        }

        private static string GetScopeName(IMetadataScope scope)
        {
            var asm = scope as AssemblyNameReference;
            if (asm != null)
            {
                return asm.Name;
            }
            var mod = scope as ModuleDefinition;
            if (mod != null)
            {
                return mod.Assembly.Name.Name;
            }
            return scope.Name;
        }

        public int GetHashCode(T obj)
        {
            return obj.FullName.GetHashCode() ^ GetScopeName(obj.Scope).GetHashCode();
        }
    }
}