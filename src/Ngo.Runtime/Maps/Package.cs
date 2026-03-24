using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Maps
{
    [GoPackage("maps")]
    public static class Package
    {
        public static Map<K, V> Clone<K, V>(Map<K, V> m) where K : notnull
        {
            var result = new Map<K, V>();
            foreach (var kvp in m)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        public static void Copy<K, V>(Map<K, V> dst, Map<K, V> src) where K : notnull
        {
            foreach (var kvp in src)
            {
                dst[kvp.Key] = kvp.Value;
            }
        }

        public static void DeleteFunc<K, V>(Map<K, V> m, System.Func<K, V, bool> del) where K : notnull
        {
            var keys = new System.Collections.Generic.List<K>();
            foreach (var kvp in m)
            {
                if (del(kvp.Key, kvp.Value))
                    keys.Add(kvp.Key);
            }
            foreach (var key in keys)
            {
                m.Delete(key);
            }
        }

        public static bool Equal<K, V>(Map<K, V> m1, Map<K, V> m2) where K : notnull
        {
            if (m1.Len != m2.Len) return false;
            foreach (var kvp in m1)
            {
                var (v2, ok) = m2.Get(kvp.Key);
                if (!ok || !object.Equals(kvp.Value, v2))
                    return false;
            }
            return true;
        }

        [GoFunc]
        public static bool EqualFunc(object? m1, object? m2, object? eq) => false;
    }
}
