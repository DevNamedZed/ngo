// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sort
{
    [GoPackage("sort")]
    public static class Package
    {
        // sort.Ints(a []int)
        [GoFunc]
        public static void Ints(Slice<long> s)
        {
            var span = s.AsSpan();
            span.Sort();
        }

        // sort.Strings(a []string)
        [GoFunc]
        public static void Strings(Slice<string> s)
        {
            var span = s.AsSpan();
            span.Sort();
        }

        // sort.Float64s(a []float64)
        [GoFunc]
        public static void Float64s(Slice<double> s)
        {
            var span = s.AsSpan();
            span.Sort();
        }

        // sort.IntsAreSorted(a []int) bool
        [GoFunc]
        public static bool IntsAreSorted(Slice<long> s)
        {
            for (int i = 1; i < s.Len; i++)
            {
                if (s[i] < s[i - 1])
                    return false;
            }
            return true;
        }

        // sort.StringsAreSorted(a []string) bool
        [GoFunc]
        public static bool StringsAreSorted(Slice<string> s)
        {
            for (int i = 1; i < s.Len; i++)
            {
                if (string.Compare(s[i], s[i - 1], StringComparison.Ordinal) < 0)
                    return false;
            }
            return true;
        }

        // sort.Float64sAreSorted(a []float64) bool
        [GoFunc]
        public static bool Float64sAreSorted(Slice<double> s)
        {
            for (int i = 1; i < s.Len; i++)
            {
                if (s[i] < s[i - 1])
                    return false;
            }
            return true;
        }

        // sort.SearchInts(a []int, x int) int
        [GoFunc]
        public static long SearchInts(Slice<long> s, long x)
        {
            int lo = 0, hi = s.Len;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (s[mid] < x)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return (long)lo;
        }

        // sort.SearchStrings(a []string, x string) int
        [GoFunc]
        public static long SearchStrings(Slice<string> s, string x)
        {
            int lo = 0, hi = s.Len;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (string.Compare(s[mid], x, StringComparison.Ordinal) < 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return (long)lo;
        }

        // sort.Sort(data Interface)
        [GoFunc]
        public static void Sort([GoParam("Interface")] Interface data)
        {
            long n = data.Len();
            QuickSort(data, 0, n - 1);
        }

        // sort.Stable(data Interface)
        [GoFunc]
        public static void Stable([GoParam("Interface")] Interface data)
        {
            long n = data.Len();
            InsertionSort(data, 0, n);
        }

        // sort.Reverse(data Interface) Interface
        [GoFunc]
        [return: GoReturn("Interface")]
        public static Interface Reverse([GoParam("Interface")] Interface data)
        {
            return new ReverseWrapper(data);
        }

        // sort.IsSorted(data Interface) bool
        [GoFunc]
        public static bool IsSorted([GoParam("Interface")] Interface data)
        {
            long n = data.Len();
            for (long i = 1; i < n; i++)
            {
                if (data.Less(i, i - 1))
                    return false;
            }
            return true;
        }

        // sort.Search(n int, f func(int) bool) int
        [GoFunc]
        public static long Search(long n, [GoParam("func(int) bool")] Func<long, bool> f)
        {
            long lo = 0, hi = n;
            while (lo < hi)
            {
                long mid = lo + (hi - lo) / 2;
                if (!f(mid))
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        // sort.Slice(x interface{}, less func(i, j int) bool)
        [GoFunc]
        public static void Slice([GoParam("interface{}")] object slice, [GoParam("func(int, int) bool")] Func<long, long, bool> less)
        {
            var sliceType = slice.GetType();
            var lenProp = sliceType.GetProperty("Len");
            if (lenProp == null) return;
            int n = (int)lenProp.GetValue(slice)!;
            if (n <= 1) return;

            var swapMethod = sliceType.GetMethod("Swap");
            if (swapMethod == null) return;

            // Quicksort using less and swap
            SortRange(slice, swapMethod, less, 0, n - 1);
        }

        private static void SortRange(object slice, System.Reflection.MethodInfo swap,
            Func<long, long, bool> less, int lo, int hi)
        {
            if (lo >= hi) return;

            // Partition
            int pivot = lo;
            int i = lo + 1;
            int j = hi;
            while (i <= j)
            {
                while (i <= hi && less(i, pivot))
                    i++;
                while (j > lo && less(pivot, j))
                    j--;
                if (i < j)
                {
                    swap.Invoke(slice, new object[] { i, j });
                    i++;
                    j--;
                }
                else
                {
                    break;
                }
            }
            if (j != pivot)
                swap.Invoke(slice, new object[] { pivot, j });

            SortRange(slice, swap, less, lo, j - 1);
            SortRange(slice, swap, less, j + 1, hi);
        }

        // sort.SliceStable(x interface{}, less func(i, j int) bool)
        [GoFunc]
        public static void SliceStable([GoParam("interface{}")] object slice, [GoParam("func(int, int) bool")] Func<long, long, bool> less)
        {
            Slice(slice, less);
        }

        // sort.SliceIsSorted(x interface{}, less func(i, j int) bool) bool
        [GoFunc]
        public static bool SliceIsSorted([GoParam("interface{}")] object slice, [GoParam("func(int, int) bool")] Func<long, long, bool> less)
        {
            var sliceType = slice.GetType();
            var lenProp = sliceType.GetProperty("Len");
            if (lenProp == null) return true;
            int n = (int)lenProp.GetValue(slice)!;

            for (long i = 1; i < n; i++)
            {
                if (less(i, i - 1))
                    return false;
            }
            return true;
        }

        // sort.Find(n int, cmp func(int) int) (i int, found bool)
        [GoFunc]
        [return: GoReturn("int", "bool")]
        public static (long, bool) Find(long n, [GoParam("func(int) int")] Func<long, long> cmp)
        {
            long lo = 0, hi = n;
            while (lo < hi)
            {
                long mid = lo + (hi - lo) / 2;
                long c = cmp(mid);
                if (c > 0)
                    lo = mid + 1;
                else if (c < 0)
                    hi = mid;
                else
                    return (mid, true);
            }
            return (lo, false);
        }

        // Helper: quicksort for Interface
        private static void QuickSort(Interface data, long lo, long hi)
        {
            if (lo >= hi) return;

            long pivot = lo;
            long i = lo + 1;
            long j = hi;
            while (i <= j)
            {
                while (i <= hi && data.Less(i, pivot))
                    i++;
                while (j > lo && data.Less(pivot, j))
                    j--;
                if (i < j)
                {
                    data.Swap(i, j);
                    i++;
                    j--;
                }
                else
                {
                    break;
                }
            }
            if (j != pivot)
                data.Swap(pivot, j);

            QuickSort(data, lo, j - 1);
            QuickSort(data, j + 1, hi);
        }

        // Helper: insertion sort for Stable
        private static void InsertionSort(Interface data, long lo, long hi)
        {
            for (long i = lo + 1; i < hi; i++)
            {
                for (long j = i; j > lo && data.Less(j, j - 1); j--)
                {
                    data.Swap(j, j - 1);
                }
            }
        }

        // Helper: reverse wrapper for Reverse()
        private class ReverseWrapper : Interface
        {
            private readonly Interface _data;
            public ReverseWrapper(Interface data) { _data = data; }
            public long Len() => _data.Len();
            public bool Less(long i, long j) => _data.Less(j, i);
            public void Swap(long i, long j) => _data.Swap(i, j);
        }
    }
}
