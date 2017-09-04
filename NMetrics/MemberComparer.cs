using System.Collections.Generic;
using System.Globalization;
using Mono.Cecil;

namespace NMetrics
{
    public class MemberComparer<T> : IComparer<T>
        where T : MemberReference
    {
        public int Compare(T x, T y)
        {
            return CultureInfo.InvariantCulture.CompareInfo.Compare(x?.FullName, y?.FullName);
        }
    }
}