// -----------------------------------------------------------------------
// <copyright file="GoStringSlice.cs" company="Ziad">
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

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sort
{
    [GoType("named", Name = "StringSlice", Package = "sort", Underlying = "[]string")]
    public sealed class GoStringSlice : IGoSortInterface
    {
        public Slice<GoString> Slice;

        public GoStringSlice()
        {
            Slice = default;
        }

        public GoStringSlice(Slice<GoString> slice)
        {
            Slice = slice;
        }

        [GoMethod]
        public long Len()
        {
            return Slice.Len;
        }

        [GoMethod]
        public bool Less(long i, long j)
        {
            return Slice[(int)i].CompareTo(Slice[(int)j]) < 0;
        }

        [GoMethod]
        public void Swap(long i, long j)
        {
            Slice.Swap((int)i, (int)j);
        }

        [GoMethod]
        public void Sort()
        {
            Package.Sort(this);
        }

        [GoMethod]
        public long Search(GoString target)
        {
            int low = 0;
            int high = Slice.Len;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (Slice[mid].CompareTo(target) < 0)
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
