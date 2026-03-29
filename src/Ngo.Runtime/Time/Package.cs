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
using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Time
{
    [GoPackage("time")]
    public static class GoTime
    {
        // Duration constants (nanoseconds) — Go type is time.Duration (named int64)
        [GoConst(Type = "Duration")]
        public const long Nanosecond = 1;
        [GoConst(Type = "Duration")]
        public const long Microsecond = 1000;
        [GoConst(Type = "Duration")]
        public const long Millisecond = 1_000_000;
        [GoConst(Type = "Duration")]
        public const long Second = 1_000_000_000;
        [GoConst(Type = "Duration")]
        public const long Minute = 60 * Second;
        [GoConst(Type = "Duration")]
        public const long Hour = 60 * Minute;

        // Layout constants
        [GoConst]
        public const string RFC3339 = "2006-01-02T15:04:05Z07:00";
        [GoConst]
        public const string RFC3339Nano = "2006-01-02T15:04:05.999999999Z07:00";
        [GoConst]
        public const string RFC822 = "02 Jan 06 15:04 MST";
        [GoConst]
        public const string RFC822Z = "02 Jan 06 15:04 -0700";
        [GoConst]
        public const string RFC850 = "Monday, 02-Jan-06 15:04:05 MST";
        [GoConst]
        public const string RFC1123 = "Mon, 02 Jan 2006 15:04:05 MST";
        [GoConst]
        public const string RFC1123Z = "Mon, 02 Jan 2006 15:04:05 -0700";
        [GoConst]
        public const string Kitchen = "3:04PM";
        [GoConst]
        public const string Stamp = "Jan _2 15:04:05";
        [GoConst]
        public const string StampMilli = "Jan _2 15:04:05.000";
        [GoConst]
        public const string StampMicro = "Jan _2 15:04:05.000000";
        [GoConst]
        public const string StampNano = "Jan _2 15:04:05.000000000";
        [GoConst]
        public const string DateTime = "2006-01-02 15:04:05";
        [GoConst]
        public const string DateOnly = "2006-01-02";
        [GoConst]
        public const string TimeOnly = "15:04:05";
        [GoConst]
        public const string ANSIC = "Mon Jan _2 15:04:05 2006";
        [GoConst]
        public const string UnixDate = "Mon Jan _2 15:04:05 MST 2006";
        [GoConst]
        public const string RubyDate = "Mon Jan 02 15:04:05 -0700 2006";

        // Month constants — Go type is time.Month (named int)
        [GoConst(Type = "Month")]
        public const long January = 1;
        [GoConst(Type = "Month")]
        public const long February = 2;
        [GoConst(Type = "Month")]
        public const long March = 3;
        [GoConst(Type = "Month")]
        public const long April = 4;
        [GoConst(Type = "Month")]
        public const long May = 5;
        [GoConst(Type = "Month")]
        public const long June = 6;
        [GoConst(Type = "Month")]
        public const long July = 7;
        [GoConst(Type = "Month")]
        public const long August = 8;
        [GoConst(Type = "Month")]
        public const long September = 9;
        [GoConst(Type = "Month")]
        public const long October = 10;
        [GoConst(Type = "Month")]
        public const long November = 11;
        [GoConst(Type = "Month")]
        public const long December = 12;

        // Weekday constants — Go type is time.Weekday (named int)
        [GoConst(Type = "Weekday")]
        public const long Sunday = 0;
        [GoConst(Type = "Weekday")]
        public const long Monday = 1;
        [GoConst(Type = "Weekday")]
        public const long Tuesday = 2;
        [GoConst(Type = "Weekday")]
        public const long Wednesday = 3;
        [GoConst(Type = "Weekday")]
        public const long Thursday = 4;
        [GoConst(Type = "Weekday")]
        public const long Friday = 5;
        [GoConst(Type = "Weekday")]
        public const long Saturday = 6;

        [GoVar(Type = "*Location")]
        public static readonly object UTC = new GoLocation("UTC", System.TimeZoneInfo.Utc);

        [GoVar(Type = "*Location")]
        public static readonly object Local = new GoLocation("Local", System.TimeZoneInfo.Local);

        // time.Sleep(d Duration)
        [GoFunc]
        public static void Sleep([GoParam("Duration")] long nanoseconds)
        {
            int ms = (int)(nanoseconds / 1_000_000);
            if (ms > 0)
            {
                Thread.Sleep(ms);
            }
        }

        // time.Now() Time
        [GoFunc]
        [return: GoReturn("Time")]
        public static GoTimeValue Now()
        {
            return new GoTimeValue(DateTimeOffset.UtcNow);
        }

        // time.Since(t Time) Duration
        [GoFunc]
        [return: GoReturn("Duration")]
        public static long Since([GoParam("Time")] GoTimeValue t)
        {
            var elapsed = DateTimeOffset.UtcNow - t.Value;
            return (long)(elapsed.TotalMilliseconds * 1_000_000);
        }

        // time.Until(t Time) Duration
        [GoFunc]
        [return: GoReturn("Duration")]
        public static long Until([GoParam("Time")] GoTimeValue t)
        {
            var diff = t.Value - DateTimeOffset.UtcNow;
            return (long)(diff.TotalMilliseconds * 1_000_000);
        }

        // time.Date(year int, month Month, day, hour, min, sec, nsec int, loc *Location) Time
        [GoFunc]
        [return: GoReturn("Time")]
        public static GoTimeValue Date(
            [GoParam("int")] long year,
            [GoParam("int")] long month,
            [GoParam("int")] long day,
            [GoParam("int")] long hour,
            [GoParam("int")] long min,
            [GoParam("int")] long sec,
            [GoParam("int")] long nsec,
            [GoParam("*Location")] object? loc)
        {
            var dto = new DateTimeOffset((int)year, (int)month, (int)day,
                (int)hour, (int)min, (int)sec, TimeSpan.Zero);
            dto = dto.AddTicks(nsec / 100);
            return new GoTimeValue(dto);
        }

        // time.Parse(layout, value string) (Time, error)
        [GoFunc]
        [return: GoReturn("Time", "error")]
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

        // time.ParseInLocation(layout, value string, loc *Location) (Time, error)
        [GoFunc]
        [return: GoReturn("Time", "error")]
        public static (GoTimeValue, object?) ParseInLocation(string layout, string value, [GoParam("*Location")] object? loc)
        {
            // Parse the time, then apply location
            var (result, err) = Parse(layout, value);
            if (err != null)
            {
                return (result, err);
            }

            // If a location is provided, apply its timezone
            if (loc is GoLocation goLoc && goLoc.TimeZone != null)
            {
                try
                {
                    var converted = TimeZoneInfo.ConvertTime(result.Value, goLoc.TimeZone);
                    return (new GoTimeValue(converted), null);
                }
                catch
                {
                    // Fall back to parsed time if timezone conversion fails
                }
            }

            return (result, null);
        }

        // time.ParseDuration(s string) (Duration, error)
        [GoFunc]
        [return: GoReturn("Duration", "error")]
        public static (long, object?) ParseDuration(string s)
        {
            // Simple parser for Go duration strings like "1h30m", "500ms", "2s"
            try
            {
                long total = 0;
                int i = 0;
                bool negative = false;
                if (i < s.Length && s[i] == '-')
                {
                    negative = true;
                    i++;
                }
                if (i < s.Length && s[i] == '+')
                {
                    i++;
                }
                while (i < s.Length)
                {
                    // Parse number (may have decimal)
                    int numStart = i;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.'))
                        i++;
                    if (i == numStart)
                        return (0, $"time: invalid duration \"{s}\"");
                    double num = double.Parse(s.Substring(numStart, i - numStart),
                        System.Globalization.CultureInfo.InvariantCulture);

                    // Parse unit
                    int unitStart = i;
                    while (i < s.Length && !char.IsDigit(s[i]) && s[i] != '.' && s[i] != '-' && s[i] != '+')
                        i++;
                    string unit = s.Substring(unitStart, i - unitStart);
                    long multiplier = unit switch
                    {
                        "ns" => 1,
                        "us" or "\u00b5s" => 1000,
                        "ms" => 1_000_000,
                        "s" => 1_000_000_000,
                        "m" => 60_000_000_000,
                        "h" => 3_600_000_000_000,
                        _ => throw new Exception($"time: unknown unit \"{unit}\" in duration \"{s}\""),
                    };
                    total += (long)(num * multiplier);
                }
                if (negative) total = -total;
                return (total, null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        // time.Unix(sec, nsec int64) Time
        [GoFunc]
        [return: GoReturn("Time")]
        public static GoTimeValue Unix([GoParam("int64")] long sec, [GoParam("int64")] long nsec)
        {
            var dto = DateTimeOffset.FromUnixTimeSeconds(sec);
            dto = dto.AddTicks(nsec / 100);
            return new GoTimeValue(dto);
        }

        // time.UnixMilli(msec int64) Time
        [GoFunc]
        [return: GoReturn("Time")]
        public static GoTimeValue UnixMilli([GoParam("int64")] long msec)
        {
            return new GoTimeValue(DateTimeOffset.FromUnixTimeMilliseconds(msec));
        }

        // time.UnixMicro(usec int64) Time
        [GoFunc]
        [return: GoReturn("Time")]
        public static GoTimeValue UnixMicro([GoParam("int64")] long usec)
        {
            var dto = DateTimeOffset.FromUnixTimeMilliseconds(usec / 1000);
            dto = dto.AddTicks((usec % 1000) * 10);
            return new GoTimeValue(dto);
        }

        // time.NewTimer(d Duration) *Timer
        [GoFunc]
        [return: GoReturn("*Timer")]
        public static GoTimer NewTimer([GoParam("Duration")] long d)
        {
            return new GoTimer(d);
        }

        // time.NewTicker(d Duration) *Ticker
        [GoFunc]
        [return: GoReturn("*Ticker")]
        public static GoTicker NewTicker([GoParam("Duration")] long d)
        {
            return new GoTicker(d);
        }

        // time.After(d Duration) <-chan Time
        [GoFunc]
        [return: GoReturn("<-chan Time")]
        public static object After([GoParam("Duration")] long d)
        {
            var ch = new Channel<GoTimeValue>(1);
            var durationMs = d / Millisecond;
            if (durationMs < 1)
            {
                durationMs = 1;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                System.Threading.Thread.Sleep((int)durationMs);
                ch.TrySend(GoTime.Now());
            });
            return ch;
        }

        // time.Tick(d Duration) <-chan Time
        [GoFunc]
        [return: GoReturn("<-chan Time")]
        public static object Tick([GoParam("Duration")] long d)
        {
            if (d <= 0)
            {
                return null!;
            }
            var ticker = NewTicker(d);
            return ticker.C_chan;
        }

        // time.AfterFunc(d Duration, f func()) *Timer
        [GoFunc]
        [return: GoReturn("*Timer")]
        public static GoTimer AfterFunc([GoParam("Duration")] long d, [GoParam("func()")] Action f)
        {
            var durationMs = d / Millisecond;
            if (durationMs < 1)
            {
                durationMs = 1;
            }
            var timer = new GoTimer(d);
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                System.Threading.Thread.Sleep((int)durationMs);
                f();
            });
            return timer;
        }

        // time.LoadLocation(name string) (*Location, error)
        [GoFunc]
        [return: GoReturn("*Location", "error")]
        public static (GoLocation?, object?) LoadLocation(string name)
        {
            try
            {
                if (name == "UTC" || name == "")
                {
                    return (new GoLocation("UTC", TimeZoneInfo.Utc), null);
                }
                if (name == "Local")
                {
                    return (new GoLocation("Local", TimeZoneInfo.Local), null);
                }
                var tz = TimeZoneInfo.FindSystemTimeZoneById(name);
                return (new GoLocation(name, tz), null);
            }
            catch
            {
                return (null, $"unknown time zone {name}");
            }
        }

        // time.FixedZone(name string, offset int) *Location
        [GoFunc]
        [return: GoReturn("*Location")]
        public static GoLocation FixedZone(string name, [GoParam("int")] long offset)
        {
            return new GoLocation(name, TimeSpan.FromSeconds(offset));
        }

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
}
