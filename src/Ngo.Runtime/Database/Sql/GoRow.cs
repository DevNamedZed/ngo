using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "Row", Package = "database/sql")]
    public class GoRow
    {
        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(params object?[] dest) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Err() => null;
    }
}
