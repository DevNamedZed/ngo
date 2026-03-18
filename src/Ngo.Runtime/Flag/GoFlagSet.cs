using System;
using System.Collections.Generic;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Flag
{
    [GoType("struct", Name = "FlagSet", Package = "flag")]
    public class GoFlagSet
    {
        private readonly string _name;
        private readonly long _errorHandling;
        private readonly Dictionary<string, FlagEntry> _flags = new Dictionary<string, FlagEntry>();
        private readonly List<string> _args = new List<string>();
        private bool _parsed;

        public GoFlagSet(string name, long errorHandling)
        {
            _name = name;
            _errorHandling = errorHandling;
        }

        public GoFlagSet() : this("", 0) { }

        [GoMethod]
        public object? Parse(Slice<string> arguments)
        {
            _parsed = true;
            _args.Clear();

            int i = 0;
            while (i < arguments.Len)
            {
                string arg = arguments[i];
                if (!arg.StartsWith("-"))
                {
                    for (int j = i; j < arguments.Len; j++)
                    {
                        _args.Add(arguments[j]);
                    }
                    break;
                }

                string flagName = arg.TrimStart('-');

                // Handle --flag=value
                string? inlineValue = null;
                int eqIdx = flagName.IndexOf('=');
                if (eqIdx >= 0)
                {
                    inlineValue = flagName.Substring(eqIdx + 1);
                    flagName = flagName.Substring(0, eqIdx);
                }

                i++;

                if (_flags.TryGetValue(flagName, out var entry))
                {
                    string value = inlineValue ?? (i < arguments.Len ? arguments[i++] : "true");

                    switch (entry.Type)
                    {
                        case FlagType.String:
                            if (entry.StringPtr != null)
                            {
                                entry.StringPtr.Value = value;
                            }
                            break;
                        case FlagType.Int:
                            if (entry.IntPtr != null && long.TryParse(value, out var iv))
                            {
                                entry.IntPtr.Value = iv;
                            }
                            break;
                        case FlagType.Bool:
                            if (entry.BoolPtr != null)
                            {
                                if (inlineValue != null)
                                {
                                    entry.BoolPtr.Value = bool.TryParse(value, out var bv) && bv;
                                }
                                else
                                {
                                    entry.BoolPtr.Value = true;
                                    i--; // bool flags don't consume the next arg
                                }
                            }
                            break;
                        case FlagType.Float64:
                            if (entry.Float64Ptr != null && double.TryParse(value,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var fv))
                            {
                                entry.Float64Ptr.Value = fv;
                            }
                            break;
                    }
                }
                else if (inlineValue == null && i < arguments.Len && !arguments[i].StartsWith("-"))
                {
                    i++; // Skip unknown flag's value
                }
            }
            return null;
        }

        [GoMethod]
        public Slice<string> Args() => new Slice<string>(_args.ToArray());

        [GoMethod]
        public long NArg() => _args.Count;

        [GoMethod]
        public void Visit(Action<GoFlag> fn)
        {
            foreach (var kv in _flags)
            {
                fn(new GoFlag { Name = kv.Key });
            }
        }

        [GoMethod]
        public void VisitAll(Action<GoFlag> fn)
        {
            foreach (var kv in _flags)
            {
                fn(new GoFlag { Name = kv.Key });
            }
        }

        [GoMethod]
        public GoFlag? Lookup(string name)
        {
            if (_flags.ContainsKey(name))
            {
                return new GoFlag { Name = name };
            }
            return null;
        }

        [GoMethod]
        public object? Set(string name, string value) => null;

        [GoMethod]
        public FlagStringPtr String(string name, string defaultValue, string usage)
        {
            var ptr = new FlagStringPtr(defaultValue);
            _flags[name] = new FlagEntry { Type = FlagType.String, StringPtr = ptr };
            return ptr;
        }

        [GoMethod]
        public void BoolVar(Ptr<bool> p, string name, bool defaultValue, string usage)
        {
            p.Value = defaultValue;
            _flags[name] = new FlagEntry { Type = FlagType.Bool, BoolPtr = p };
        }

        [GoMethod]
        public void Var(object value, string name, string usage) { }

        [GoField(Name = "Usage")]
        public Action? Usage;

        [GoMethod]
        public void StringVar(object p, string name, string value, string usage)
        {
            if (p is FlagStringPtr sp)
            {
                sp.Value = value;
                _flags[name] = new FlagEntry { Type = FlagType.String, StringPtr = sp };
            }
        }

        [GoMethod]
        public void IntVar(Ptr<long> p, string name, long value, string usage)
        {
            p.Value = value;
            _flags[name] = new FlagEntry { Type = FlagType.Int, IntPtr = p };
        }

        [GoMethod]
        public void Float64Var(Ptr<double> p, string name, double value, string usage)
        {
            p.Value = value;
            _flags[name] = new FlagEntry { Type = FlagType.Float64, Float64Ptr = p };
        }

        [GoMethod]
        public void DurationVar(Ptr<long> p, string name, long value, string usage)
        {
            p.Value = value;
            _flags[name] = new FlagEntry { Type = FlagType.Int, IntPtr = p };
        }

        [GoMethod]
        public void Int64Var(Ptr<long> p, string name, long value, string usage)
        {
            p.Value = value;
            _flags[name] = new FlagEntry { Type = FlagType.Int, IntPtr = p };
        }

        [GoMethod]
        [return: GoReturn("*bool")]
        public Ptr<bool> Bool(string name, bool value, string usage)
        {
            var ptr = new Ptr<bool>(value);
            _flags[name] = new FlagEntry { Type = FlagType.Bool, BoolPtr = ptr };
            return ptr;
        }

        [GoMethod]
        [return: GoReturn("*int")]
        public Ptr<long> Int(string name, long value, string usage)
        {
            var ptr = new Ptr<long>(value);
            _flags[name] = new FlagEntry { Type = FlagType.Int, IntPtr = ptr };
            return ptr;
        }

        [GoMethod]
        [return: GoReturn("*int64")]
        public Ptr<long> Int64(string name, long value, string usage)
        {
            var ptr = new Ptr<long>(value);
            _flags[name] = new FlagEntry { Type = FlagType.Int, IntPtr = ptr };
            return ptr;
        }

        [GoMethod]
        [return: GoReturn("*float64")]
        public Ptr<double> Float64(string name, double value, string usage)
        {
            var ptr = new Ptr<double>(value);
            _flags[name] = new FlagEntry { Type = FlagType.Float64, Float64Ptr = ptr };
            return ptr;
        }

        [GoMethod]
        public bool Parsed() => _parsed;

        [GoMethod]
        public string Name() => _name;

        [GoMethod]
        public long NFlag() => _flags.Count;

        [GoMethod]
        public string Arg(long i)
        {
            if (i >= 0 && i < _args.Count)
            {
                return _args[(int)i];
            }
            return "";
        }

        [GoMethod]
        public void PrintDefaults() { }

        [GoMethod]
        public void SetOutput(object? w) { }

        [GoMethod]
        public object? Output() => null;

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
