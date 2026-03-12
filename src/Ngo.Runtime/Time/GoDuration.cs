// -----------------------------------------------------------------------
// <copyright file="GoDuration.cs" company="Ziad">
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

using System.Text;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Time
{
    // time.Duration — named type backed by int64
    [GoType("named", Name = "Duration", Underlying = "int64", Package = "time")]
    public struct GoDuration
    {
        public long Value;

        public GoDuration(long value) { Value = value; }

        [GoMethod]
        public string String()
        {
            long ns = Value;
            if (ns == 0) return "0s";
            var sb = new StringBuilder();
            if (ns < 0) { sb.Append('-'); ns = -ns; }
            if (ns >= 3_600_000_000_000)
            {
                sb.Append(ns / 3_600_000_000_000);
                sb.Append('h');
                ns %= 3_600_000_000_000;
            }
            if (ns >= 60_000_000_000)
            {
                sb.Append(ns / 60_000_000_000);
                sb.Append('m');
                ns %= 60_000_000_000;
            }
            if (ns >= 1_000_000_000)
            {
                sb.Append(ns / 1_000_000_000);
                sb.Append('s');
            }
            else if (ns >= 1_000_000)
            {
                sb.Append(ns / 1_000_000);
                sb.Append("ms");
            }
            else if (ns >= 1000)
            {
                sb.Append(ns / 1000);
                sb.Append("\u00b5s");
            }
            else if (ns > 0)
            {
                sb.Append(ns);
                sb.Append("ns");
            }
            return sb.ToString();
        }

        [GoMethod]
        [return: GoReturn("int64")]
        public long Nanoseconds() => Value;

        [GoMethod]
        [return: GoReturn("int64")]
        public long Microseconds() => Value / 1000;

        [GoMethod]
        [return: GoReturn("int64")]
        public long Milliseconds() => Value / 1_000_000;

        [GoMethod]
        public double Seconds() => (double)Value / 1_000_000_000;

        [GoMethod]
        public double Minutes() => (double)Value / 60_000_000_000;

        [GoMethod]
        public double Hours() => (double)Value / 3_600_000_000_000;

        [GoMethod]
        [return: GoReturn("Duration")]
        public long Truncate([GoParam("Duration")] long m)
        {
            if (m <= 0) return Value;
            return Value - Value % m;
        }

        [GoMethod]
        [return: GoReturn("Duration")]
        public long Round([GoParam("Duration")] long m)
        {
            if (m <= 0) return Value;
            long r = Value % m;
            if (Value < 0)
            {
                if (-r + r > m) return Value - m - r;
                return Value - r;
            }
            if (r + r > m) return Value + m - r;
            return Value - r;
        }

        [GoMethod]
        [return: GoReturn("Duration")]
        public long Abs()
        {
            return Value < 0 ? -Value : Value;
        }

        public static implicit operator long(GoDuration d) => d.Value;
        public static implicit operator GoDuration(long v) => new GoDuration(v);

        public override string ToString() => String();
    }
}
