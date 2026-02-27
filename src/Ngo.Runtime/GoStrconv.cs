// -----------------------------------------------------------------------
// <copyright file="GoStrconv.cs" company="Ziad">
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
using System.Globalization;

namespace Ngo.Runtime
{
    public static class GoStrconv
    {
        public static string Itoa(long i) => i.ToString();

        public static (long value, object? error) Atoi(string s)
        {
            if (long.TryParse(s, out long result))
            {
                return (result, null);
            }
            return (0, $"strconv.Atoi: parsing \"{s}\": invalid syntax");
        }

        public static string FormatInt(long i, long @base)
        {
            return (int)@base switch
            {
                2 => Convert.ToString(i, 2),
                8 => Convert.ToString(i, 8),
                16 => Convert.ToString(i, 16),
                _ => i.ToString(),
            };
        }

        public static string FormatBool(bool b) => b ? "true" : "false";

        public static (long value, object? error) ParseInt(string s, long @base, long bitSize)
        {
            try
            {
                long result = Convert.ToInt64(s, (int)(@base == 0 ? 10 : @base));
                return (result, null);
            }
            catch
            {
                return (0, $"strconv.ParseInt: parsing \"{s}\": invalid syntax");
            }
        }

        public static (double value, object? error) ParseFloat(string s, long bitSize)
        {
            if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out double result))
            {
                return (result, null);
            }
            return (0.0, $"strconv.ParseFloat: parsing \"{s}\": invalid syntax");
        }

        public static string FormatFloat(double f, byte fmt, long prec, long bitSize)
        {
            return fmt switch
            {
                (byte)'f' => prec >= 0
                    ? f.ToString("F" + prec, CultureInfo.InvariantCulture)
                    : f.ToString("G", CultureInfo.InvariantCulture),
                (byte)'e' => prec >= 0
                    ? f.ToString("E" + prec, CultureInfo.InvariantCulture).ToLower()
                    : f.ToString("E", CultureInfo.InvariantCulture).ToLower(),
                (byte)'E' => prec >= 0
                    ? f.ToString("E" + prec, CultureInfo.InvariantCulture)
                    : f.ToString("E", CultureInfo.InvariantCulture),
                (byte)'g' => prec >= 0
                    ? f.ToString("G" + prec, CultureInfo.InvariantCulture)
                    : f.ToString("G", CultureInfo.InvariantCulture),
                _ => f.ToString("G", CultureInfo.InvariantCulture),
            };
        }

        public static bool ParseBool(string s)
        {
            return s switch
            {
                "1" or "t" or "T" or "TRUE" or "true" or "True" => true,
                _ => false,
            };
        }
    }
}
