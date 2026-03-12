using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "NamedArg", Package = "database/sql")]
    public struct GoNamedArg
    {
        [GoField(Name = "Name")]
        public string Name;

        [GoField(Name = "Value")]
        public object? Value;
    }
}
