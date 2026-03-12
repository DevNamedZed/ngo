using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Cmp
{
    /// <summary>
    /// Runtime support for Go's cmp package.
    /// </summary>
    [GoPackage("cmp")]
    public static class Package
    {
        [GoFunc]
        public static long Compare(object x, object y)
        {
            if (x is IComparable cx)
                return cx.CompareTo(y);
            return 0;
        }

        [GoFunc]
        public static bool Less(object x, object y)
        {
            return Compare(x, y) < 0;
        }

        [GoFunc(IsVariadic = true)]
        public static object Or(params object[] vals)
        {
            foreach (var v in vals)
            {
                if (v != null && !IsZero(v))
                    return v;
            }
            return vals.Length > 0 ? vals[0] : null!;
        }

        private static bool IsZero(object v)
        {
            if (v is string s) return s.Length == 0;
            if (v is long l) return l == 0;
            if (v is int i) return i == 0;
            if (v is double d) return d == 0;
            if (v is float f) return f == 0;
            return false;
        }
    }

    // cmp.Ordered constraint interface
    [GoType("interface", Name = "Ordered", Package = "cmp")]
    public interface IOrdered
    {
    }
}
