using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Flag
{
    // flag.FlagSet struct
    [GoType("struct", Name = "FlagSet", Package = "flag")]
    public class GoFlagSet
    {
        private readonly string _name;
        private readonly long _errorHandling;

        public GoFlagSet(string name, long errorHandling)
        {
            _name = name;
            _errorHandling = errorHandling;
        }

        public GoFlagSet() : this("", 0) { }

        [GoMethod]
        public object? Parse(Slice<string> arguments) => null;

        [GoMethod]
        public Slice<string> Args() => new Slice<string>(Array.Empty<string>());

        [GoMethod]
        public long NArg() => 0;

        [GoMethod]
        public void Visit(Action<GoFlag> fn) { }

        [GoMethod]
        public void VisitAll(Action<GoFlag> fn) { }

        [GoMethod]
        public GoFlag? Lookup(string name) => null;

        [GoMethod]
        public object? Set(string name, string value) => null;

        [GoMethod]
        public FlagStringPtr String(string name, string defaultValue, string usage)
        {
            return new FlagStringPtr(defaultValue);
        }

        [GoMethod]
        public void BoolVar(Ptr<bool> p, string name, bool defaultValue, string usage)
        {
            // Stub
        }

        [GoMethod]
        public void Var(object value, string name, string usage)
        {
            // Stub: registers a flag with a Value interface
        }

        [GoField(Name = "Usage")]
        public Action? Usage;

        [GoMethod]
        public void StringVar(object p, string name, string value, string usage)
        {
            // Stub: string pointers are reference types
        }

        [GoMethod]
        public void IntVar(Ptr<long> p, string name, long value, string usage)
        {
            p.Value = value;
        }

        [GoMethod]
        public void BoolVar2(Ptr<bool> p, string name, bool value, string usage)
        {
            p.Value = value;
        }

        [GoMethod]
        public void Int64Var(Ptr<long> p, string name, long value, string usage)
        {
            p.Value = value;
        }

        [GoMethod]
        public void Float64Var(Ptr<double> p, string name, double value, string usage)
        {
            p.Value = value;
        }

        [GoMethod]
        public void DurationVar(Ptr<long> p, string name, long value, string usage)
        {
            p.Value = value;
        }

        [GoMethod]
        [return: GoReturn("*bool")]
        public Ptr<bool> Bool(string name, bool value, string usage)
        {
            return new Ptr<bool>(value);
        }

        [GoMethod]
        [return: GoReturn("*int")]
        public Ptr<long> Int(string name, long value, string usage)
        {
            return new Ptr<long>(value);
        }

        [GoMethod]
        [return: GoReturn("*int64")]
        public Ptr<long> Int64(string name, long value, string usage)
        {
            return new Ptr<long>(value);
        }

        [GoMethod]
        [return: GoReturn("*float64")]
        public Ptr<double> Float64(string name, double value, string usage)
        {
            return new Ptr<double>(value);
        }

        [GoMethod]
        public bool Parsed() => false;

        [GoMethod]
        public string Name() => _name;

        [GoMethod]
        public long NFlag() => 0;

        [GoMethod]
        public string Arg(long i) => "";

        [GoMethod]
        public void PrintDefaults() { }

        [GoMethod]
        public void SetOutput(object? w) { }

        [GoMethod]
        public object? Output() => null;
    }
}
