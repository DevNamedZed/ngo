// -----------------------------------------------------------------------
// <copyright file="GoWeekday.cs" company="Ziad">
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
    // time.Weekday — named type backed by int
    [GoType("named", Name = "Weekday", Underlying = "int", Package = "time")]
    public struct GoWeekday
    {
        public long Value;

        public GoWeekday(long value) { Value = value; }

        private static readonly string[] _dayNames = {
            "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
        };

        [GoMethod]
        public string String()
        {
            if (Value >= 0 && Value <= 6) return _dayNames[Value];
            return $"Weekday({Value})";
        }

        public static implicit operator long(GoWeekday w) => w.Value;
        public static implicit operator GoWeekday(long v) => new GoWeekday(v);

        public override string ToString() => String();
    }
}
