using System.Collections.Generic;
using Mono.Cecil;

namespace NMetrics
{
    public class MemberEqualityComparer<T> : IEqualityComparer<T>
        where T : MemberReference
    {
        public bool Equals(T x, T y)
        {
            if (x == null || y == null)
            {
                return false;
            }
            return x.FullName.Equals(y.FullName);
        }

        public int GetHashCode(T obj)
        {
            return obj?.FullName.GetHashCode() ?? 0;
        }
    }
}