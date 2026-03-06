// -----------------------------------------------------------------------
// <copyright file="TextSpan.cs" company="Ziad">
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

namespace Ngo.Compiler.Language
{
    public readonly struct TextSpan : IEquatable<TextSpan>
    {
        public TextSpan(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public int Start { get; }

        public int Length { get; }

        public int End => Start + Length;

        public bool Contains(int position) => position >= Start && position < End;

        public bool OverlapsWith(TextSpan other) => Start < other.End && other.Start < End;

        public static TextSpan FromBounds(int start, int end) => new TextSpan(start, end - start);

        public bool Equals(TextSpan other) => Start == other.Start && Length == other.Length;

        public override bool Equals(object? obj) => obj is TextSpan other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Start, Length);

        public override string ToString() => $"[{Start}..{End})";

        public static bool operator ==(TextSpan left, TextSpan right) => left.Equals(right);

        public static bool operator !=(TextSpan left, TextSpan right) => !left.Equals(right);
    }
}
