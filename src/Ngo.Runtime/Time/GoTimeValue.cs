// -----------------------------------------------------------------------
// <copyright file="GoTimeValue.cs" company="Ziad">
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

namespace Ngo.Runtime.Time
{
    // time.Time — struct type
    [GoType("struct", Package = "time", Name = "Time")]
    public sealed class GoTimeValue
    {
        public DateTimeOffset Value { get; }

        public GoTimeValue(DateTimeOffset value)
        {
            Value = value;
        }

        [GoMethod]
        [return: GoReturn("int64")]
        public long Unix() => Value.ToUnixTimeSeconds();

        [GoMethod]
        [return: GoReturn("int64")]
        public long UnixMilli() => Value.ToUnixTimeMilliseconds();

        [GoMethod]
        [return: GoReturn("int64")]
        public long UnixMicro() => Value.ToUnixTimeMilliseconds() * 1000 + (Value.Ticks % TimeSpan.TicksPerMillisecond) / 10;

        [GoMethod]
        [return: GoReturn("int64")]
        public long UnixNano() => Value.Ticks * 100; // 1 tick = 100 ns

        [GoMethod]
        public string String()
        {
            return Value.ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz");
        }

        [GoMethod]
        public string Format(string layout)
        {
            var fmt = GoTime.GoTimeLayout(layout);
            try
            {
                return Value.ToString(fmt);
            }
            catch
            {
                return Value.ToString();
            }
        }

        [GoMethod]
        [return: GoReturn("Duration")]
        public long Sub([GoParam("Time")] GoTimeValue u)
        {
            var diff = Value - u.Value;
            return (long)(diff.TotalMilliseconds * 1_000_000);
        }

        [GoMethod]
        [return: GoReturn("Time")]
        public GoTimeValue Add([GoParam("Duration")] long d)
        {
            return new GoTimeValue(Value.AddTicks(d / 100));
        }

        [GoMethod]
        public bool Before([GoParam("Time")] GoTimeValue u) => Value < u.Value;

        [GoMethod]
        public bool After([GoParam("Time")] GoTimeValue u) => Value > u.Value;

        [GoMethod]
        public bool Equal([GoParam("Time")] GoTimeValue u) => Value == u.Value;

        [GoMethod]
        [return: GoReturn("int")]
        public long Compare([GoParam("Time")] GoTimeValue u)
        {
            if (Value < u.Value) return -1;
            if (Value > u.Value) return 1;
            return 0;
        }

        [GoMethod]
        public bool IsZero() => Value == DateTimeOffset.MinValue;

        [GoMethod]
        [return: GoReturn("int")]
        public long Year() => Value.Year;

        [GoMethod]
        [return: GoReturn("Month")]
        public long Month() => Value.Month;

        [GoMethod]
        [return: GoReturn("int")]
        public long Day() => Value.Day;

        [GoMethod]
        [return: GoReturn("int")]
        public long Hour() => Value.Hour;

        [GoMethod]
        [return: GoReturn("int")]
        public long Minute() => Value.Minute;

        [GoMethod]
        [return: GoReturn("int")]
        public long Second() => Value.Second;

        [GoMethod]
        [return: GoReturn("int")]
        public long Nanosecond() => (Value.Ticks % TimeSpan.TicksPerSecond) * 100;

        [GoMethod]
        [return: GoReturn("Weekday")]
        public long Weekday() => (long)Value.DayOfWeek;

        [GoMethod]
        [return: GoReturn("*Location")]
        public GoLocation? Location()
        {
            return Value.Offset == TimeSpan.Zero
                ? new GoLocation("UTC", TimeSpan.Zero)
                : new GoLocation("", Value.Offset);
        }

        [GoMethod(Name = "UTC")]
        [return: GoReturn("Time")]
        public GoTimeValue UtcTime()
        {
            return new GoTimeValue(Value.ToUniversalTime());
        }

        [GoMethod(Name = "Local")]
        [return: GoReturn("Time")]
        public GoTimeValue LocalTime()
        {
            return new GoTimeValue(Value.ToLocalTime());
        }

        [GoMethod]
        [return: GoReturn("Time")]
        public GoTimeValue In([GoParam("*Location")] GoLocation? loc)
        {
            if (loc == null) return new GoTimeValue(Value.ToUniversalTime());
            return new GoTimeValue(Value.ToOffset(loc.Offset));
        }

        [GoMethod]
        [return: GoReturn("Time")]
        public GoTimeValue Truncate([GoParam("Duration")] long d)
        {
            if (d <= 0) return this;
            // Truncate to nearest multiple of d from zero time
            long ns = UnixNano();
            long rem = ns % d;
            if (rem < 0) rem += d;
            return new GoTimeValue(Value.AddTicks(-rem / 100));
        }

        [GoMethod]
        [return: GoReturn("Time")]
        public GoTimeValue Round([GoParam("Duration")] long d)
        {
            if (d <= 0) return this;
            long ns = UnixNano();
            long rem = ns % d;
            if (rem < 0) rem += d;
            if (rem * 2 >= d)
                return new GoTimeValue(Value.AddTicks((d - rem) / 100));
            return new GoTimeValue(Value.AddTicks(-rem / 100));
        }

        [GoMethod]
        [return: GoReturn("Time")]
        public GoTimeValue AddDate([GoParam("int")] long years, [GoParam("int")] long months, [GoParam("int")] long days)
        {
            var dt = Value.DateTime.AddYears((int)years).AddMonths((int)months).AddDays((int)days);
            return new GoTimeValue(new DateTimeOffset(dt, Value.Offset));
        }

        [GoMethod]
        [return: GoReturn("int", "int", "int")]
        public (long, long, long) Clock()
        {
            return (Value.Hour, Value.Minute, Value.Second);
        }

        [GoMethod(Name = "Date")]
        [return: GoReturn("int", "Month", "int")]
        public (long, long, long) DateComponents()
        {
            return (Value.Year, Value.Month, Value.Day);
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long YearDay() => Value.DayOfYear;

        [GoMethod]
        [return: GoReturn("int", "int")]
        public (long, long) ISOWeek()
        {
            var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            int week = cal.GetWeekOfYear(Value.DateTime,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            int year = Value.Year;
            // Adjust for edge cases
            if (Value.Month == 12 && week == 1) year++;
            if (Value.Month == 1 && week >= 52) year--;
            return (year, week);
        }

        [GoMethod]
        [return: GoReturn("string", "int")]
        public (string, long) Zone()
        {
            string name = Value.Offset == TimeSpan.Zero ? "UTC" : Value.Offset.ToString();
            long offset = (long)Value.Offset.TotalSeconds;
            return (name, offset);
        }

        [GoMethod]
        [return: GoReturn("[]byte")]
        public byte[] AppendFormat([GoParam("[]byte")] byte[] b, string layout)
        {
            string formatted = Format(layout);
            byte[] fmtBytes = global::System.Text.Encoding.UTF8.GetBytes(formatted);
            byte[] result = new byte[b.Length + fmtBytes.Length];
            Array.Copy(b, result, b.Length);
            Array.Copy(fmtBytes, 0, result, b.Length, fmtBytes.Length);
            return result;
        }

        [GoMethod]
        public string GoString()
        {
            return $"time.Date({Value.Year}, time.Month({Value.Month}), {Value.Day}, {Value.Hour}, {Value.Minute}, {Value.Second}, {Nanosecond()}, time.UTC)";
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (byte[], object?) GobEncode()
        {
            return (global::System.Text.Encoding.UTF8.GetBytes(Value.ToString("o")), null);
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (byte[], object?) MarshalJSON()
        {
            string json = $"\"{Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz")}\"";
            return (global::System.Text.Encoding.UTF8.GetBytes(json), null);
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (byte[], object?) MarshalText()
        {
            return (global::System.Text.Encoding.UTF8.GetBytes(Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz")), null);
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (byte[], object?) MarshalBinary()
        {
            return (global::System.Text.Encoding.UTF8.GetBytes(Value.ToString("o")), null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? UnmarshalBinary([GoParam("[]byte")] byte[] data)
        {
            // Stub: no-op for sealed class
            return null;
        }

        public override string ToString() => String();
    }
}
