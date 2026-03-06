// -----------------------------------------------------------------------
// <copyright file="GoTime.cs" company="Ziad">
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
using System.Threading;

namespace Ngo.Runtime
{
    public static class GoTime
    {
        // Go's time.Duration is int64 nanoseconds
        // time.Sleep takes a Duration (nanoseconds)
        public static void Sleep(long nanoseconds)
        {
            int ms = (int)(nanoseconds / 1_000_000);
            if (ms > 0)
            {
                Thread.Sleep(ms);
            }
        }

        // time.Now() Time
        public static GoTimeValue Now()
        {
            return new GoTimeValue(DateTimeOffset.UtcNow);
        }

        // time.Since(t Time) Duration
        public static long Since(GoTimeValue t)
        {
            var elapsed = DateTimeOffset.UtcNow - t.Value;
            return (long)(elapsed.TotalMilliseconds * 1_000_000);
        }

        // Duration constants (nanoseconds)
        public const long Nanosecond = 1;
        public const long Microsecond = 1000;
        public const long Millisecond = 1_000_000;
        public const long Second = 1_000_000_000;
        public const long Minute = 60 * Second;
        public const long Hour = 60 * Minute;

        // time.Date(year, month, day, hour, min, sec, nsec int, loc string) Time
        public static GoTimeValue Date(long year, long month, long day,
            long hour, long min, long sec, long nsec, object? loc)
        {
            var dto = new DateTimeOffset((int)year, (int)month, (int)day,
                (int)hour, (int)min, (int)sec, TimeSpan.Zero);
            dto = dto.AddTicks(nsec / 100);
            return new GoTimeValue(dto);
        }

        // time.Parse(layout, value string) (Time, error)
        public static (GoTimeValue, object?) Parse(string layout, string value)
        {
            try
            {
                var fmt = GoTimeLayout(layout);
                if (DateTimeOffset.TryParseExact(value, fmt, null,
                    System.Globalization.DateTimeStyles.None, out var dto))
                {
                    return (new GoTimeValue(dto), null);
                }
                if (DateTimeOffset.TryParse(value, out dto))
                {
                    return (new GoTimeValue(dto), null);
                }
                return (new GoTimeValue(DateTimeOffset.MinValue),
                    $"parsing time \"{value}\" as \"{layout}\": cannot parse");
            }
            catch (Exception ex)
            {
                return (new GoTimeValue(DateTimeOffset.MinValue), ex.Message);
            }
        }

        // time.Unix(sec, nsec int64) Time
        public static GoTimeValue Unix(long sec, long nsec)
        {
            var dto = DateTimeOffset.FromUnixTimeSeconds(sec);
            dto = dto.AddTicks(nsec / 100);
            return new GoTimeValue(dto);
        }

        // time.UTC — placeholder (null represents UTC)
        public static object? UTC => null;

        // Layout constants
        public const string RFC3339 = "2006-01-02T15:04:05Z07:00";
        public const string RFC822 = "02 Jan 06 15:04 MST";
        public const string Kitchen = "3:04PM";
        public const string DateTime = "2006-01-02 15:04:05";
        public const string DateOnly = "2006-01-02";
        public const string TimeOnly = "15:04:05";

        internal static string GoTimeLayout(string layout)
        {
            return layout
                .Replace("2006", "yyyy")
                .Replace("01", "MM")
                .Replace("02", "dd")
                .Replace("15", "HH")
                .Replace("04", "mm")
                .Replace("05", "ss")
                .Replace("Z07:00", "zzz")
                .Replace("-07:00", "zzz")
                .Replace("Z07", "zz")
                .Replace("MST", "zzz");
        }
    }

    public sealed class GoTimeValue
    {
        public DateTimeOffset Value { get; }

        public GoTimeValue(DateTimeOffset value)
        {
            Value = value;
        }

        public long Unix() => Value.ToUnixTimeSeconds();

        public long UnixMilli() => Value.ToUnixTimeMilliseconds();

        public long UnixNano() => Value.Ticks * 100; // 1 tick = 100 ns

        public string String()
        {
            return Value.ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz");
        }

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

        public long Sub(GoTimeValue u)
        {
            var diff = Value - u.Value;
            return (long)(diff.TotalMilliseconds * 1_000_000);
        }

        public GoTimeValue Add(long d)
        {
            return new GoTimeValue(Value.AddTicks(d / 100));
        }

        public bool Before(GoTimeValue u) => Value < u.Value;

        public bool After(GoTimeValue u) => Value > u.Value;

        public bool Equal(GoTimeValue u) => Value == u.Value;

        public bool IsZero() => Value == DateTimeOffset.MinValue;

        public long Year() => Value.Year;

        public long Month() => Value.Month;

        public long Day() => Value.Day;

        public long Hour() => Value.Hour;

        public long Minute() => Value.Minute;

        public long Second() => Value.Second;

        public long Nanosecond() => (Value.Ticks % TimeSpan.TicksPerSecond) * 100;

        public long Weekday() => (long)Value.DayOfWeek;

        public override string ToString() => String();
    }
}
