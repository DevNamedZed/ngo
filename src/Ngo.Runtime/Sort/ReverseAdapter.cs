// -----------------------------------------------------------------------
// <copyright file="ReverseAdapter.cs" company="Ziad">
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

namespace Ngo.Runtime.Sort
{
    /// <summary>
    /// Wraps an IGoSortInterface and reverses the Less comparison,
    /// implementing Go's sort.Reverse(data).
    /// </summary>
    internal sealed class ReverseAdapter : IGoSortInterface
    {
        private readonly IGoSortInterface _inner;

        public ReverseAdapter(IGoSortInterface inner)
        {
            _inner = inner;
        }

        public long Len() => _inner.Len();

        public bool Less(long i, long j) => _inner.Less(j, i);

        public void Swap(long i, long j) => _inner.Swap(i, j);
    }
}
