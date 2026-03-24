using System;
using System.Data.Common;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "Stmt", Package = "database/sql")]
    public class GoStmt
    {
        private readonly DbCommand? _cmd;

        public GoStmt() { }

        internal GoStmt(DbCommand cmd)
        {
            _cmd = cmd;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close()
        {
            _cmd?.Dispose();
            return null;
        }

        [GoMethod]
        [return: GoReturn("Result", "error")]
        public (object?, object?) Exec(params object?[] args)
        {
            if (_cmd == null)
            {
                return (null, "sql: statement is closed");
            }
            try
            {
                int affected = _cmd.ExecuteNonQuery();
                return (new GoExecResult(affected), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("*Rows", "error")]
        public (GoRows?, object?) Query(params object?[] args)
        {
            if (_cmd == null)
            {
                return (null, "sql: statement is closed");
            }
            try
            {
                var reader = _cmd.ExecuteReader();
                return (new GoRows(reader, _cmd), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("*Row")]
        public GoRow QueryRow(params object?[] args)
        {
            var (rows, err) = Query(args);
            return new GoRow(rows, err);
        }

        [GoMethod]
        [return: GoReturn("*sql.Rows", "error")]
        public (object?, object?) QueryContext([GoParam("context.Context")] object? ctx, [GoParam("...interface{}")] params object[] args) => (null, null);

        [GoMethod]
        [return: GoReturn("sql.Result", "error")]
        public (object?, object?) ExecContext([GoParam("context.Context")] object? ctx, [GoParam("...interface{}")] params object[] args) => (null, null);
    }
}
