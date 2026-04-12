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
        public static void Sort(IGoSortInterface data)
        {
            if (data == null)
            {
                return;
            }

            int length = (int)data.Len();
            if (length <= 1)
            {
                return;
            }

            InterfaceQuickSort(data, 0, length - 1);
        }

        public static void Stable(IGoSortInterface data)
        {
            Sort(data);
        }

        public static bool IsSorted(IGoSortInterface data)
        {
            if (data == null)
            {
                return true;
            }

            int length = (int)data.Len();
            for (int i = 1; i < length; i++)
            {
                if (data.Less(i, i - 1))
                {
                    return false;
                }
            }
            return true;
        }

        public static IGoSortInterface Reverse(IGoSortInterface data)
        {
            return new ReverseAdapter(data);
        }

        /// <summary>
        /// Go sort.Find: binary search using a comparison function.
        /// Returns the smallest index i in [0, n) at which cmp(i) &lt;= 0,
        /// and reports whether cmp(i) == 0.
        /// </summary>
        public static (long, bool) Find(long count, Func<long, long> cmp)
        {
            long low = 0;
            long high = count;
            while (low < high)
            {
                long mid = low + (high - low) / 2;
                if (cmp(mid) > 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            bool found = low < count && cmp(low) == 0;
            return (low, found);
        }

        /// <summary>
        /// Go sort.Search: binary search returning the smallest index i in [0, n)
        /// at which f(i) is true.
        /// </summary>
        public static long Search(long count, Func<long, bool> predicate)
        {
            long low = 0;
            long high = count;
            while (low < high)
            {
                long mid = low + (high - low) / 2;
                if (!predicate(mid))
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }
            return low;
        }

        public static void Ints(Slice<long> slice)
        {
            SortSlice(slice, (a, b) => a.CompareTo(b));
        }

        public static void Strings(Slice<GoString> slice)
        {
            SortSlice(slice, (a, b) => a.CompareTo(b));
        }

        public static void Float64s(Slice<double> slice)
        {
            SortSlice(slice, (a, b) => a.CompareTo(b));
        }

        public static bool IntsAreSorted(Slice<long> slice)
        {
            return IsSorted(slice, (a, b) => a.CompareTo(b));
        }

        public static bool StringsAreSorted(Slice<string> slice)
        {
            return IsSorted(slice, (a, b) => string.Compare(a, b, StringComparison.Ordinal));
        }

        public static bool Float64sAreSorted(Slice<double> slice)
        {
            return IsSorted(slice, (a, b) => a.CompareTo(b));
        }

        public static long SearchInts(Slice<long> slice, long value)
        {
            return BinarySearch(slice, value, (a, b) => a.CompareTo(b));
        }

        public static long SearchStrings(Slice<string> slice, string value)
        {
            return BinarySearch(slice, value, (a, b) => string.Compare(a, b, StringComparison.Ordinal));
        }

        private static void SortSlice<T>(Slice<T> slice, Comparison<T> comparison)
        {
            if (slice.Len <= 1)
            {
                return;
            }

            QuickSort(slice, 0, slice.Len - 1, comparison);
        }

        private static void QuickSort<T>(Slice<T> slice, int low, int high, Comparison<T> comparison)
        {
            while (low < high)
            {
                if (high - low < 12)
                {
                    InsertionSort(slice, low, high, comparison);
                    return;
                }

                int pivot = Partition(slice, low, high, comparison);
                if (pivot - low < high - pivot)
                {
                    QuickSort(slice, low, pivot - 1, comparison);
                    low = pivot + 1;
                }
                else
                {
                    QuickSort(slice, pivot + 1, high, comparison);
                    high = pivot - 1;
                }
            }
        }

        private static void InsertionSort<T>(Slice<T> slice, int low, int high, Comparison<T> comparison)
        {
            for (int i = low + 1; i <= high; i++)
            {
                T key = slice[i];
                int j = i - 1;
                while (j >= low && comparison(slice[j], key) > 0)
                {
                    slice[j + 1] = slice[j];
                    j--;
                }
                slice[j + 1] = key;
            }
        }

        private static int Partition<T>(Slice<T> slice, int low, int high, Comparison<T> comparison)
        {
            int mid = low + (high - low) / 2;
            if (comparison(slice[mid], slice[low]) < 0)
            {
                slice.Swap(low, mid);
            }
            if (comparison(slice[high], slice[low]) < 0)
            {
                slice.Swap(low, high);
            }
            if (comparison(slice[mid], slice[high]) < 0)
            {
                slice.Swap(mid, high);
            }

            T pivot = slice[high];
            int store = low;
            for (int i = low; i < high; i++)
            {
                if (comparison(slice[i], pivot) < 0)
                {
                    slice.Swap(i, store);
                    store++;
                }
            }
            slice.Swap(store, high);
            return store;
        }

        private static bool IsSorted<T>(Slice<T> slice, Comparison<T> comparison)
        {
            for (int i = 1; i < slice.Len; i++)
            {
                if (comparison(slice[i], slice[i - 1]) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Go sort.Slice: sorts a slice given a less(i, j int) bool function.
        /// Takes object (boxed Slice&lt;T&gt;) and casts to ISliceOps for Swap/Len.
        /// </summary>
        public static void Slice(object sliceObj, Func<long, long, bool> less)
        {
            if (sliceObj is not ISliceOps ops || ops.Len <= 1)
            {
                return;
            }

            QuickSortByIndex(ops, less, 0, ops.Len - 1);
        }

        /// <summary>
        /// Go sort.SliceStable: stable sort via less function. Uses binary-insertion
        /// merge sort so equal elements retain their original relative order.
        /// </summary>
        public static void SliceStable(object sliceObj, Func<long, long, bool> less)
        {
            if (sliceObj is not ISliceOps ops || ops.Len <= 1)
            {
                return;
            }

            StableSortByIndex(ops, less, 0, ops.Len);
        }

        private static void StableSortByIndex(ISliceOps ops, Func<long, long, bool> less, int low, int high)
        {
            int length = high - low;
            if (length < 2)
            {
                return;
            }

            const int runSize = 16;
            for (int start = low; start < high; start += runSize)
            {
                int end = System.Math.Min(start + runSize, high);
                InsertionSortStable(ops, less, start, end);
            }

            for (int width = runSize; width < length; width *= 2)
            {
                for (int leftStart = low; leftStart < high - width; leftStart += 2 * width)
                {
                    int mid = leftStart + width;
                    int rightEnd = System.Math.Min(leftStart + 2 * width, high);
                    MergeStable(ops, less, leftStart, mid, rightEnd);
                }
            }
        }

        private static void InsertionSortStable(ISliceOps ops, Func<long, long, bool> less, int low, int high)
        {
            for (int i = low + 1; i < high; i++)
            {
                for (int j = i; j > low && less(j, j - 1); j--)
                {
                    ops.Swap(j, j - 1);
                }
            }
        }

        private static void MergeStable(ISliceOps ops, Func<long, long, bool> less, int low, int mid, int high)
        {
            int left = low;
            int right = mid;
            while (left < right && right < high)
            {
                if (less(right, left))
                {
                    int target = right;
                    while (target > left)
                    {
                        ops.Swap(target, target - 1);
                        target--;
                    }
                    left++;
                    right++;
                    mid++;
                }
                else
                {
                    left++;
                }
            }
        }

        /// <summary>
        /// Go sort.SliceIsSorted: reports whether a slice is sorted according to less.
        /// </summary>
        public static bool SliceIsSorted(object sliceObj, Func<long, long, bool> less)
        {
            if (sliceObj is not ISliceOps ops)
            {
                return true;
            }

            for (int i = 1; i < ops.Len; i++)
            {
                if (less(i, i - 1))
                {
                    return false;
                }
            }
            return true;
        }

        private static void QuickSortByIndex(ISliceOps ops, Func<long, long, bool> less,
            int low, int high)
        {
            while (low < high)
            {
                if (high - low < 12)
                {
                    InsertionSortByIndex(ops, less, low, high);
                    return;
                }

                int pivot = PartitionByIndex(ops, less, low, high);
                if (pivot - low < high - pivot)
                {
                    QuickSortByIndex(ops, less, low, pivot - 1);
                    low = pivot + 1;
                }
                else
                {
                    QuickSortByIndex(ops, less, pivot + 1, high);
                    high = pivot - 1;
                }
            }
        }

        private static void InsertionSortByIndex(ISliceOps ops, Func<long, long, bool> less,
            int low, int high)
        {
            for (int i = low + 1; i <= high; i++)
            {
                for (int j = i; j > low && less(j, j - 1); j--)
                {
                    ops.Swap(j, j - 1);
                }
            }
        }

        private static int PartitionByIndex(ISliceOps ops, Func<long, long, bool> less,
            int low, int high)
        {
            int mid = low + (high - low) / 2;
            if (less(mid, low))
            {
                ops.Swap(low, mid);
            }
            if (less(high, low))
            {
                ops.Swap(low, high);
            }
            if (less(mid, high))
            {
                ops.Swap(mid, high);
            }

            int store = low;
            for (int i = low; i < high; i++)
            {
                if (less(i, high))
                {
                    ops.Swap(i, store);
                    store++;
                }
            }
            ops.Swap(store, high);
            return store;
        }

        private static void InterfaceQuickSort(IGoSortInterface data, int low, int high)
        {
            while (low < high)
            {
                if (high - low < 12)
                {
                    for (int i = low + 1; i <= high; i++)
                    {
                        for (int j = i; j > low && data.Less(j, j - 1); j--)
                        {
                            data.Swap(j, j - 1);
                        }
                    }
                    return;
                }

                int mid = low + (high - low) / 2;
                if (data.Less(mid, low))
                {
                    data.Swap(low, mid);
                }
                if (data.Less(high, low))
                {
                    data.Swap(low, high);
                }
                if (data.Less(mid, high))
                {
                    data.Swap(mid, high);
                }

                int store = low;
                for (int i = low; i < high; i++)
                {
                    if (data.Less(i, high))
                    {
                        data.Swap(i, store);
                        store++;
                    }
                }
                data.Swap(store, high);

                if (store - low < high - store)
                {
                    InterfaceQuickSort(data, low, store - 1);
                    low = store + 1;
                }
                else
                {
                    InterfaceQuickSort(data, store + 1, high);
                    high = store - 1;
                }
            }
        }

        private static long BinarySearch<T>(Slice<T> slice, T value, Comparison<T> comparison)
        {
            int low = 0;
            int high = slice.Len;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (comparison(slice[mid], value) < 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }
            return low;
        }
    }
}
