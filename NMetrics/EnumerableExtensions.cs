using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMetrics
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Traverse<T>(this IEnumerable<T> init, Func<T, IEnumerable<T>> next, 
            bool includeInit = true, IEqualityComparer<T> comparer = null)
        {
            var processed = new HashSet<T>(comparer);
            var currentLevel = init.ToHashSet(comparer);
            var firstLevel = true;

            while (currentLevel.Any())
            {
                var nextLevel = currentLevel.SelectMany(next).ToHashSet(comparer);
                if (includeInit || !firstLevel)
                {
                    processed.UnionWith(currentLevel);
                }
                firstLevel = false;
                currentLevel = nextLevel;
                currentLevel.ExceptWith(processed);
            }
            return processed;
        }

        //public static IEnumerable<T> Traversal<T>(this IEnumerable<T> init, 
        //    Func<T, IEnumerable<T>>

        public static IEnumerable<T> AsEnumerableWithOneItem<T>(this T obj)
        {
            if (obj != null)
            {
                yield return obj;
            }
        }

        public static IEnumerable<T> Ordered<T>(this IEnumerable<T> collection, IComparer<T> comparer = null)
        {
            return collection.OrderBy(x => x, comparer);
        }

    }
}
