using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "Rows", Package = "database/sql")]
    public class GoRows
    {
        [GoMethod]
        public bool Next() => false;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(params object?[] dest) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Err() => null;

        [GoMethod]
        [return: GoReturn("[]string", "error")]
        public (Slice<string>, object?) Columns() => (new Slice<string>(Array.Empty<string>()), null);
    }
}
