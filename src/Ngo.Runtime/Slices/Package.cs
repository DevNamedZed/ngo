using System;
using System.Collections.Generic;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Slices
{
    /// <summary>
    /// Runtime support for Go's slices package.
    /// </summary>
    [GoPackage("slices")]
    public static class Package
    {
        [GoFunc]
        public static void Sort<T>(Slice<T> x) where T : IComparable<T>
        {
            var arr = x.AsSpan().ToArray();
            Array.Sort(arr);
            for (int i = 0; i < arr.Length; i++)
                x[i] = arr[i];
        }

        [GoFunc]
        public static void SortFunc<T>(Slice<T> x, Func<T, T, long> cmp)
        {
            var arr = x.AsSpan().ToArray();
            Array.Sort(arr, (a, b) => (int)cmp(a, b));
            for (int i = 0; i < arr.Length; i++)
                x[i] = arr[i];
        }

        [GoFunc]
        public static void SortStableFunc<T>(Slice<T> x, Func<T, T, long> cmp)
        {
            var list = new List<T>();
            for (int i = 0; i < x.Len; i++)
                list.Add(x[i]);
            list.Sort((a, b) => (int)cmp(a, b));
            for (int i = 0; i < list.Count; i++)
                x[i] = list[i];
        }

        [GoFunc]
        public static bool Contains<T>(Slice<T> s, T v)
        {
            for (int i = 0; i < s.Len; i++)
                if (EqualityComparer<T>.Default.Equals(s[i], v))
                    return true;
            return false;
        }

        [GoFunc]
        public static bool ContainsFunc<T>(Slice<T> s, Func<T, bool> f)
        {
            for (int i = 0; i < s.Len; i++)
                if (f(s[i]))
                    return true;
            return false;
        }

        [GoFunc]
        public static long Index<T>(Slice<T> s, T v)
        {
            for (int i = 0; i < s.Len; i++)
                if (EqualityComparer<T>.Default.Equals(s[i], v))
                    return i;
            return -1;
        }

        [GoFunc]
        public static long IndexFunc<T>(Slice<T> s, Func<T, bool> f)
        {
            for (int i = 0; i < s.Len; i++)
                if (f(s[i]))
                    return i;
            return -1;
        }

        [GoFunc]
        public static Slice<T> Compact<T>(Slice<T> s)
        {
            if (s.Len == 0) return s;
            var result = new List<T> { s[0] };
            for (int i = 1; i < s.Len; i++)
                if (!EqualityComparer<T>.Default.Equals(s[i], s[i - 1]))
                    result.Add(s[i]);
            return new Slice<T>(result.ToArray());
        }

        [GoFunc]
        public static Slice<T> CompactFunc<T>(Slice<T> s, Func<T, T, bool> eq)
        {
            if (s.Len == 0) return s;
            var result = new List<T> { s[0] };
            for (int i = 1; i < s.Len; i++)
                if (!eq(s[i - 1], s[i]))
                    result.Add(s[i]);
            return new Slice<T>(result.ToArray());
        }

        [GoFunc]
        public static Slice<T> Clone<T>(Slice<T> s)
        {
            var arr = s.AsSpan().ToArray();
            return new Slice<T>(arr);
        }

        [GoFunc]
        public static void Reverse<T>(Slice<T> s)
        {
            for (int i = 0, j = s.Len - 1; i < j; i++, j--)
            {
                var tmp = s[i];
                s[i] = s[j];
                s[j] = tmp;
            }
        }

        [GoFunc]
        public static bool Equal<T>(Slice<T> s1, Slice<T> s2)
        {
            if (s1.Len != s2.Len) return false;
            for (int i = 0; i < s1.Len; i++)
                if (!EqualityComparer<T>.Default.Equals(s1[i], s2[i]))
                    return false;
            return true;
        }

        [GoFunc]
        public static bool EqualFunc<T1, T2>(Slice<T1> s1, Slice<T2> s2, Func<T1, T2, bool> eq)
        {
            if (s1.Len != s2.Len) return false;
            for (int i = 0; i < s1.Len; i++)
                if (!eq(s1[i], s2[i]))
                    return false;
            return true;
        }

        [GoFunc]
        public static Slice<T> Delete<T>(Slice<T> s, long i, long j)
        {
            var list = new List<T>();
            for (int k = 0; k < s.Len; k++)
                if (k < i || k >= j)
                    list.Add(s[k]);
            return new Slice<T>(list.ToArray());
        }

        [GoFunc(IsVariadic = true)]
        public static Slice<T> Insert<T>(Slice<T> s, long i, params T[] v)
        {
            var list = new List<T>();
            for (int k = 0; k < s.Len; k++)
            {
                if (k == i)
                    list.AddRange(v);
                list.Add(s[k]);
            }
            if (i >= s.Len)
                list.AddRange(v);
            return new Slice<T>(list.ToArray());
        }

        [GoFunc(IsVariadic = true)]
        public static Slice<T> Replace<T>(Slice<T> s, long i, long j, params T[] v)
        {
            var list = new List<T>();
            for (int k = 0; k < i; k++)
                list.Add(s[k]);
            list.AddRange(v);
            for (int k = (int)j; k < s.Len; k++)
                list.Add(s[k]);
            return new Slice<T>(list.ToArray());
        }

        [GoFunc]
        public static Slice<T> Grow<T>(Slice<T> s, long n)
        {
            return s;
        }

        [GoFunc]
        public static Slice<T> Clip<T>(Slice<T> s)
        {
            return s.Reslice(0, s.Len);
        }

        [GoFunc]
        public static (long, bool) BinarySearch<T>(Slice<T> x, T target) where T : IComparable<T>
        {
            int lo = 0, hi = x.Len;
            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;
                var c = x[mid].CompareTo(target);
                if (c < 0) lo = mid + 1;
                else hi = mid;
            }
            if (lo < x.Len && x[lo].CompareTo(target) == 0)
                return (lo, true);
            return (lo, false);
        }

        [GoFunc]
        public static (long, bool) BinarySearchFunc<T>(Slice<T> x, object target, Func<object, object, long> cmp)
        {
            int lo = 0, hi = x.Len;
            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;
                var c = cmp(x[mid]!, target);
                if (c < 0) lo = mid + 1;
                else hi = mid;
            }
            if (lo < x.Len && cmp(x[lo]!, target) == 0)
                return (lo, true);
            return (lo, false);
        }

        [GoFunc]
        public static bool IsSorted<T>(Slice<T> x) where T : IComparable<T>
        {
            for (int i = 1; i < x.Len; i++)
                if (x[i - 1].CompareTo(x[i]) > 0)
                    return false;
            return true;
        }

        [GoFunc]
        public static bool IsSortedFunc<T>(Slice<T> x, Func<T, T, long> cmp)
        {
            for (int i = 1; i < x.Len; i++)
                if (cmp(x[i - 1], x[i]) > 0)
                    return false;
            return true;
        }

        [GoFunc]
        public static T Min<T>(Slice<T> x) where T : IComparable<T>
        {
            if (x.Len == 0) throw new InvalidOperationException("slices.Min: empty list");
            var m = x[0];
            for (int i = 1; i < x.Len; i++)
                if (x[i].CompareTo(m) < 0)
                    m = x[i];
            return m;
        }

        [GoFunc]
        public static T Max<T>(Slice<T> x) where T : IComparable<T>
        {
            if (x.Len == 0) throw new InvalidOperationException("slices.Max: empty list");
            var m = x[0];
            for (int i = 1; i < x.Len; i++)
                if (x[i].CompareTo(m) > 0)
                    m = x[i];
            return m;
        }

        [GoFunc]
        public static T MinFunc<T>(Slice<T> x, Func<T, T, long> cmp)
        {
            if (x.Len == 0) throw new InvalidOperationException("slices.MinFunc: empty list");
            var m = x[0];
            for (int i = 1; i < x.Len; i++)
                if (cmp(x[i], m) < 0)
                    m = x[i];
            return m;
        }

        [GoFunc]
        public static T MaxFunc<T>(Slice<T> x, Func<T, T, long> cmp)
        {
            if (x.Len == 0) throw new InvalidOperationException("slices.MaxFunc: empty list");
            var m = x[0];
            for (int i = 1; i < x.Len; i++)
                if (cmp(x[i], m) > 0)
                    m = x[i];
            return m;
        }

        [GoFunc]
        public static Slice<T> DeleteFunc<T>(Slice<T> s, Func<T, bool> del)
        {
            var list = new List<T>();
            for (int i = 0; i < s.Len; i++)
                if (!del(s[i]))
                    list.Add(s[i]);
            return new Slice<T>(list.ToArray());
        }

        [GoFunc(IsVariadic = true)]
        public static Slice<T> Concat<T>(params Slice<T>[] slices)
        {
            int total = 0;
            foreach (var s in slices)
                total += s.Len;
            var arr = new T[total];
            int idx = 0;
            foreach (var s in slices)
            {
                for (int i = 0; i < s.Len; i++)
                    arr[idx++] = s[i];
            }
            return new Slice<T>(arr);
        }

        [GoFunc]
        public static Slice<T> Repeat<T>(Slice<T> x, long count)
        {
            if (count < 0) throw new InvalidOperationException("slices.Repeat: negative count");
            var arr = new T[x.Len * (int)count];
            int idx = 0;
            for (int c = 0; c < count; c++)
                for (int i = 0; i < x.Len; i++)
                    arr[idx++] = x[i];
            return new Slice<T>(arr);
        }
    }
}
