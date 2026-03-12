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
using System.Collections.Generic;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Flag
{
    [GoPackage("flag")]
    public static class Package
    {
        private static readonly Dictionary<string, FlagEntry> _flags = new();
        private static bool _parsed;
        private static readonly List<string> _args = new();

        public static FlagStringPtr String(string name, string defaultValue, string usage)
        {
            var p = new FlagStringPtr(defaultValue);
            _flags[name] = new FlagEntry { StringPtr = p, Type = FlagType.String };
            return p;
        }

        public static Ptr<long> Int(string name, long defaultValue, string usage)
        {
            var p = new Ptr<long>(defaultValue);
            _flags[name] = new FlagEntry { IntPtr = p, Type = FlagType.Int };
            return p;
        }

        public static Ptr<bool> Bool(string name, bool defaultValue, string usage)
        {
            var p = new Ptr<bool>(defaultValue);
            _flags[name] = new FlagEntry { BoolPtr = p, Type = FlagType.Bool };
            return p;
        }

        public static Ptr<double> Float64(string name, double defaultValue, string usage)
        {
            var p = new Ptr<double>(defaultValue);
            _flags[name] = new FlagEntry { Float64Ptr = p, Type = FlagType.Float64 };
            return p;
        }

        public static void Parse()
        {
            _parsed = true;
            _args.Clear();
            var cmdArgs = Environment.GetCommandLineArgs();

            // Skip first arg (executable name)
            int i = 1;
            while (i < cmdArgs.Length)
            {
                var arg = cmdArgs[i];
                if (!arg.StartsWith("-"))
                {
                    // Non-flag arg — everything after is args
                    for (int j = i; j < cmdArgs.Length; j++)
                        _args.Add(cmdArgs[j]);
                    break;
                }

                // Strip leading dashes
                var flagName = arg.TrimStart('-');
                i++;

                // Bool flags: -flag (no value) or -flag=value
                if (_flags.TryGetValue(flagName, out var entry))
                {
                    if (entry.Type == FlagType.Bool)
                    {
                        if (i < cmdArgs.Length && !cmdArgs[i].StartsWith("-"))
                        {
                            if (bool.TryParse(cmdArgs[i], out var bv))
                            {
                                entry.BoolPtr!.Value = bv;
                                i++;
                            }
                            else
                            {
                                entry.BoolPtr!.Value = true;
                            }
                        }
                        else
                        {
                            entry.BoolPtr!.Value = true;
                        }
                        continue;
                    }

                    if (i < cmdArgs.Length)
                    {
                        var val = cmdArgs[i];
                        i++;
                        switch (entry.Type)
                        {
                            case FlagType.String:
                                entry.StringPtr!.Value = val;
                                break;
                            case FlagType.Int:
                                if (long.TryParse(val, out var iv))
                                    entry.IntPtr!.Value = iv;
                                break;
                            case FlagType.Float64:
                                if (double.TryParse(val, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var fv))
                                    entry.Float64Ptr!.Value = fv;
                                break;
                        }
                    }
                }
                else
                {
                    // Unknown flag — skip value if present
                    if (i < cmdArgs.Length && !cmdArgs[i].StartsWith("-"))
                        i++;
                }
            }
        }

        public static bool Parsed()
        {
            return _parsed;
        }

        public static Slice<string> Args()
        {
            return new Slice<string>(_args.ToArray());
        }

        public static long NArg()
        {
            return (long)_args.Count;
        }

        public static string Arg(long i)
        {
            if (i >= 0 && i < _args.Count)
                return _args[(int)i];
            return "";
        }

        public static long NFlag()
        {
            return (long)_flags.Count;
        }

        public static void Var(object value, string name, string usage)
        {
            // Stub: registers a flag with a Value interface
        }

        public static Ptr<long> Uint(string name, long defaultValue, string usage)
        {
            var p = new Ptr<long>(defaultValue);
            _flags[name] = new FlagEntry { IntPtr = p, Type = FlagType.Int };
            return p;
        }

        public static Ptr<long> Int64(string name, long defaultValue, string usage)
        {
            var p = new Ptr<long>(defaultValue);
            _flags[name] = new FlagEntry { IntPtr = p, Type = FlagType.Int };
            return p;
        }

        public static Ptr<long> Uint64(string name, long defaultValue, string usage)
        {
            var p = new Ptr<long>(defaultValue);
            _flags[name] = new FlagEntry { IntPtr = p, Type = FlagType.Int };
            return p;
        }

        public static object Duration(string name, long defaultValue, string usage)
        {
            return new Ptr<long>(defaultValue);
        }

        public static Func<string>? Usage;

        public static void StringVar(FlagStringPtr p, string name, string defaultValue, string usage)
        {
        }

        public static void IntVar(Ptr<long> p, string name, long defaultValue, string usage)
        {
        }

        public static void BoolVar(Ptr<bool> p, string name, bool defaultValue, string usage)
        {
        }

        public static void Float64Var(Ptr<double> p, string name, double defaultValue, string usage)
        {
        }

        public static object CommandLine = new object(); // *FlagSet stub

        public static void PrintDefaults() { }

        public static GoFlagSet NewFlagSet(string name, long errorHandling)
        {
            return new GoFlagSet(name, errorHandling);
        }

        // flag.Lookup(name string) *Flag
        [GoFunc]
        public static GoFlag? Lookup(string name)
        {
            if (_flags.TryGetValue(name, out var entry))
                return new GoFlag { Name = name, Usage = "" };
            return null;
        }

        // ErrorHandling constants
        [GoConst] public static readonly long ContinueOnError = 0;
        [GoConst] public static readonly long ExitOnError = 1;
        [GoConst] public static readonly long PanicOnError = 2;

        private enum FlagType { String, Int, Bool, Float64 }

        private class FlagEntry
        {
            public FlagType Type;
            public FlagStringPtr? StringPtr;
            public Ptr<long>? IntPtr;
            public Ptr<bool>? BoolPtr;
            public Ptr<double>? Float64Ptr;
        }
    }
}
