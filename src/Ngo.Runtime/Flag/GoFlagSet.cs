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
    }
}
