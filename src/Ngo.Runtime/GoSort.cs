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
    }
}
