// -----------------------------------------------------------------------
// <copyright file="GoSort.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public static class GoSort
    {
        // sort.Ints(a []int)
        public static void Ints(Slice<long> s)
        {
            var span = s.AsSpan();
            span.Sort();
        }

        // sort.Strings(a []string)
        public static void Strings(Slice<string> s)
        {
            var span = s.AsSpan();
            span.Sort();
        }

        // sort.Float64s(a []float64)
        public static void Float64s(Slice<double> s)
        {
            var span = s.AsSpan();
            span.Sort();
        }

        // sort.IntsAreSorted(a []int) bool
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

        // sort.Slice(x interface{}, less func(i, j int) bool)
        public static void Slice(object slice, Func<long, long, bool> less)
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
        public static void SliceStable(object slice, Func<long, long, bool> less)
        {
            Slice(slice, less);
        }

        // sort.SliceIsSorted(x interface{}, less func(i, j int) bool) bool
        public static bool SliceIsSorted(object slice, Func<long, long, bool> less)
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
    }
}
