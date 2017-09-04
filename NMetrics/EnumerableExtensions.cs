using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMetrics
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> FillIteratively<T>(this IEnumerable<T> init, Func<T, IEnumerable<T>> func)
        {
            IEnumerable<T> all = init.ToList();
            var current = all;
            while (current.Any())
            {
                IEnumerable<T> result = new T[0];
                result = current.Aggregate(result, (x, y) => x.Union(func(y))).Except(all).ToList();
                foreach (var r in result)
                {
                    yield return r;
                }
                current = result;
            }
        }

        public static IEnumerable<T> AsEnumerableWithOneItem<T>(this T obj)
        {
            if (obj != null)
            {
                yield return obj;
            }
        }
    }
}
