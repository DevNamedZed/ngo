using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "Conn", Package = "database/sql")]
    public class GoConn
    {
        [GoMethod]
        [return: GoReturn("*sql.Tx", "error")]
        public (object?, object?) BeginTx([GoParam("context.Context")] object? ctx, [GoParam("*sql.TxOptions")] object? opts) => (null, null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? PingContext([GoParam("context.Context")] object? ctx) => null;

        [GoMethod]
        [return: GoReturn("*sql.Stmt", "error")]
        public (object?, object?) PrepareContext([GoParam("context.Context")] object? ctx, string query) => (null, null);

        [GoMethod]
        [return: GoReturn("sql.Result", "error")]
        public (object?, object?) ExecContext([GoParam("context.Context")] object? ctx, string query, [GoParam("...interface{}")] params object[] args) => (null, null);

        [GoMethod]
        [return: GoReturn("*sql.Rows", "error")]
        public (object?, object?) QueryContext([GoParam("context.Context")] object? ctx, string query, [GoParam("...interface{}")] params object[] args) => (null, null);

        [GoMethod]
        [return: GoReturn("*sql.Row")]
        public object? QueryRowContext([GoParam("context.Context")] object? ctx, string query, [GoParam("...interface{}")] params object[] args) => null;

        [GoMethod(IsVariadic = true)]
        [return: GoReturn("error")]
        public object? Raw([GoParam("func(interface{}) error")] object? f) => null;
    }
}
