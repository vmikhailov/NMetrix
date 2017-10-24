using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMetrics
{
    public static class DictionaryExtensions
    {
        public static TR GetValue<TK, TV, TR>(this IDictionary<TK, TV> dict, TK key, TR defaultValue = default(TR))
            where TV : TR
        {
            TV value;
            return dict.TryGetValue(key, out value) ? value : defaultValue;
        }

        public static TV GetValue<TK, TV>(this IDictionary<TK, TV> dict, TK key, TV defaultValue = default(TV))
        {
            TV value;
            return dict.TryGetValue(key, out value) ? value : defaultValue;
        }
    }
}
