using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "Stmt", Package = "database/sql")]
    public class GoStmt
    {
        [GoMethod]
        [return: GoReturn("error")]
        public object? Close() => null;

        [GoMethod]
        [return: GoReturn("Result", "error")]
        public (object?, object?) Exec(params object?[] args) => (null, null);

        [GoMethod]
        [return: GoReturn("*Rows", "error")]
        public (GoRows?, object?) Query(params object?[] args) => (null, null);

        [GoMethod]
        [return: GoReturn("*Row")]
        public GoRow QueryRow(params object?[] args) => new GoRow();
    }
}
