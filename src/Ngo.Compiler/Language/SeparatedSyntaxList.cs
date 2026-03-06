// -----------------------------------------------------------------------
// <copyright file="SeparatedSyntaxList.cs" company="Ziad">
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
using System.Collections;
using System.Collections.Generic;

namespace Ngo.Compiler.Language
{
    /// <summary>
    /// An immutable list of syntax nodes separated by tokens (typically commas).
    /// Internal storage is a flat list: [item0, sep0, item1, sep1, ..., itemN].
    /// </summary>
    public readonly struct SeparatedSyntaxList<T> : IEnumerable<T> where T : SyntaxNode
    {
        public static readonly SeparatedSyntaxList<T> Empty = new(Array.Empty<SyntaxNode>());

        private readonly IReadOnlyList<SyntaxNode> _nodesAndSeparators;

        public SeparatedSyntaxList(IReadOnlyList<SyntaxNode> nodesAndSeparators)
        {
            _nodesAndSeparators = nodesAndSeparators;
        }

        public int Count => (_nodesAndSeparators.Count + 1) / 2;

        public T this[int index] => (T)_nodesAndSeparators[index * 2];

        public SyntaxToken GetSeparator(int index) => (SyntaxToken)_nodesAndSeparators[index * 2 + 1];

        public int SeparatorCount => _nodesAndSeparators.Count / 2;

        /// <summary>
        /// Returns all nodes and separator tokens interleaved, for use in ChildNodes() enumeration.
        /// </summary>
        public IReadOnlyList<SyntaxNode> GetWithSeparators() => _nodesAndSeparators;

        public Enumerator GetEnumerator() => new(_nodesAndSeparators);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<T>
        {
            private readonly IReadOnlyList<SyntaxNode> _list;
            private int _index;

            internal Enumerator(IReadOnlyList<SyntaxNode> list)
            {
                _list = list;
                _index = -2; // Will be 0 after first MoveNext
            }

            public T Current => (T)_list[_index];

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                _index += 2;
                return _index < _list.Count;
            }

            public void Reset() => _index = -2;

            public void Dispose() { }
        }
    }
}
