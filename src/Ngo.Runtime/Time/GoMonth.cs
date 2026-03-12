// -----------------------------------------------------------------------
// <copyright file="GoMonth.cs" company="Ziad">
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

namespace Ngo.Runtime.Time
{
    // time.Month — named type backed by int
    [GoType("named", Name = "Month", Underlying = "int", Package = "time")]
    public struct GoMonth
    {
        public long Value;

        public GoMonth(long value) { Value = value; }

        private static readonly string[] _monthNames = {
            "", "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        [GoMethod]
        public string String()
        {
            if (Value >= 1 && Value <= 12) return _monthNames[Value];
            return $"Month({Value})";
        }

        public static implicit operator long(GoMonth m) => m.Value;
        public static implicit operator GoMonth(long v) => new GoMonth(v);

        public override string ToString() => String();
    }
}
