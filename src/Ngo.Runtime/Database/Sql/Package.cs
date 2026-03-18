using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoPackage("database/sql")]
    public static class Package
    {
        private static readonly Dictionary<string, Func<string, DbConnection>> _drivers = new Dictionary<string, Func<string, DbConnection>>();

        // sql.Scanner interface { Scan(src interface{}) error }
        [GoType("interface", Name = "Scanner", Package = "database/sql")]
        public interface IScanner
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? Scan(object? src);
        }

        // sql.Open(driverName, dataSourceName string) (*DB, error)
        [GoFunc]
        [return: GoReturn("*sql.DB", "error")]
        public static (GoDb?, object?) Open(string driverName, string dataSourceName)
        {
            if (!_drivers.TryGetValue(driverName, out var factory))
            {
                return (null, $"sql: unknown driver \"{driverName}\" (forgotten import?)");
            }

            try
            {
                var conn = factory(dataSourceName);
                return (new GoDb(conn, dataSourceName, factory), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        // sql.Register(name string, driver driver.Driver)
        [GoFunc]
        public static void Register(string name, object? driver)
        {
            // Driver registration is done via RegisterFactory for .NET interop
        }

        // RegisterFactory allows .NET code to register database drivers
        public static void RegisterFactory(string name, Func<string, DbConnection> factory)
        {
            _drivers[name] = factory;
        }

        // sql.Drivers() []string
        [GoFunc]
        [return: GoReturn("[]string")]
        public static Slice<string> Drivers()
        {
            var keys = new string[_drivers.Count];
            _drivers.Keys.CopyTo(keys, 0);
            Array.Sort(keys);
            return new Slice<string>(keys);
        }

        // sql.Named(name string, value any) NamedArg
        [GoFunc]
        [return: GoReturn("sql.NamedArg")]
        public static GoNamedArg Named(string name, object? value)
        {
            return new GoNamedArg { Name = name, Value = value };
        }

        // Error variables
        [GoVar(Type = "error")]
        public static readonly object ErrNoRows = "sql: no rows in result set";

        [GoVar(Type = "error")]
        public static readonly object ErrConnDone = "sql: connection is already closed";

        [GoVar(Type = "error")]
        public static readonly object ErrTxDone = "sql: transaction has already been committed or rolled back";

        // IsolationLevel constants
        [GoConst(Type = "sql.IsolationLevel")]
        public const long LevelDefault = 0;
        [GoConst(Type = "sql.IsolationLevel")]
        public const long LevelReadUncommitted = 1;
        [GoConst(Type = "sql.IsolationLevel")]
        public const long LevelReadCommitted = 2;
        [GoConst(Type = "sql.IsolationLevel")]
        public const long LevelWriteCommitted = 3;
        [GoConst(Type = "sql.IsolationLevel")]
        public const long LevelRepeatableRead = 4;
        [GoConst(Type = "sql.IsolationLevel")]
        public const long LevelSnapshot = 5;
        [GoConst(Type = "sql.IsolationLevel")]
        public const long LevelSerializable = 6;
        [GoConst(Type = "sql.IsolationLevel")]
        public const long LevelLinearizable = 7;
    }

    // sql.DB struct
    [GoType("struct", Name = "DB", Package = "database/sql")]
    public class GoDb
    {
        private readonly Func<string, DbConnection> _factory;
        private readonly string _connectionString;
        private DbConnection? _conn;
        private bool _closed;
        private long _maxOpenConns;
        private long _maxIdleConns = 2;
        private long _connMaxLifetime;
        private long _connMaxIdleTime;

        internal GoDb(DbConnection conn, string connStr, Func<string, DbConnection> factory)
        {
            _conn = conn;
            _connectionString = connStr;
            _factory = factory;
        }

        private DbConnection EnsureOpen()
        {
            if (_closed)
            {
                throw new InvalidOperationException("sql: database is closed");
            }
            if (_conn == null || _conn.State != ConnectionState.Open)
            {
                _conn = _factory(_connectionString);
                _conn.Open();
            }
            return _conn;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Ping()
        {
            try
            {
                var conn = EnsureOpen();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                cmd.ExecuteScalar();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? PingContext([GoParam("context.Context")] object? ctx)
        {
            return Ping();
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close()
        {
            _closed = true;
            try
            {
                _conn?.Close();
                _conn?.Dispose();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("*sql.Rows", "error")]
        public (GoRows?, object?) Query(string query, params object?[] args)
        {
            try
            {
                var conn = EnsureOpen();
                var cmd = conn.CreateCommand();
                cmd.CommandText = query;
                AddParameters(cmd, args);
                var reader = cmd.ExecuteReader();
                return (new GoRows(reader, cmd), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("*sql.Rows", "error")]
        public (GoRows?, object?) QueryContext([GoParam("context.Context")] object? ctx, string query, params object?[] args)
        {
            return Query(query, args);
        }

        [GoMethod]
        [return: GoReturn("*sql.Row")]
        public GoRow QueryRow(string query, params object?[] args)
        {
            var (rows, err) = Query(query, args);
            return new GoRow(rows, err);
        }

        [GoMethod]
        [return: GoReturn("*sql.Row")]
        public GoRow QueryRowContext([GoParam("context.Context")] object? ctx, string query, params object?[] args)
        {
            return QueryRow(query, args);
        }

        [GoMethod]
        [return: GoReturn("sql.Result", "error")]
        public (IGoResult?, object?) Exec(string query, params object?[] args)
        {
            try
            {
                var conn = EnsureOpen();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = query;
                AddParameters(cmd, args);
                int affected = cmd.ExecuteNonQuery();
                return (new GoExecResult(affected), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("sql.Result", "error")]
        public (IGoResult?, object?) ExecContext([GoParam("context.Context")] object? ctx, string query, params object?[] args)
        {
            return Exec(query, args);
        }

        [GoMethod]
        [return: GoReturn("*sql.Tx", "error")]
        public (GoTx?, object?) Begin()
        {
            try
            {
                var conn = EnsureOpen();
                var tx = conn.BeginTransaction();
                return (new GoTx(conn, tx), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("*sql.Tx", "error")]
        public (GoTx?, object?) BeginTx([GoParam("context.Context")] object? ctx, GoTxOptions? opts)
        {
            return Begin();
        }

        [GoMethod]
        [return: GoReturn("*sql.Stmt", "error")]
        public (GoStmt?, object?) Prepare(string query)
        {
            try
            {
                var conn = EnsureOpen();
                var cmd = conn.CreateCommand();
                cmd.CommandText = query;
                cmd.Prepare();
                return (new GoStmt(cmd), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("*sql.Stmt", "error")]
        public (GoStmt?, object?) PrepareContext([GoParam("context.Context")] object? ctx, string query)
        {
            return Prepare(query);
        }

        [GoMethod]
        public void SetMaxOpenConns(long n) { _maxOpenConns = n; }

        [GoMethod]
        public void SetMaxIdleConns(long n) { _maxIdleConns = n; }

        [GoMethod]
        public void SetConnMaxLifetime(long d) { _connMaxLifetime = d; }

        [GoMethod]
        public void SetConnMaxIdleTime(long d) { _connMaxIdleTime = d; }

        [GoMethod]
        [return: GoReturn("sql.DBStats")]
        public GoDbStats Stats()
        {
            return new GoDbStats
            {
                MaxOpenConnections = _maxOpenConns,
                OpenConnections = _conn != null && _conn.State == ConnectionState.Open ? 1 : 0,
                InUse = _conn != null && _conn.State == ConnectionState.Open ? 1 : 0,
            };
        }

        private static void AddParameters(DbCommand cmd, object?[]? args)
        {
            if (args == null)
            {
                return;
            }
            for (int i = 0; i < args.Length; i++)
            {
                var param = cmd.CreateParameter();
                if (args[i] is GoNamedArg named)
                {
                    param.ParameterName = named.Name;
                    param.Value = named.Value ?? DBNull.Value;
                }
                else
                {
                    param.ParameterName = $"@p{i}";
                    param.Value = args[i] ?? DBNull.Value;
                }
                cmd.Parameters.Add(param);
            }
        }
    }

    // sql.Tx struct
    [GoType("struct", Name = "Tx", Package = "database/sql")]
    public class GoTx
    {
        private readonly DbConnection _conn;
        private readonly DbTransaction _tx;
        private bool _done;

        internal GoTx(DbConnection conn, DbTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Commit()
        {
            if (_done)
            {
                return Package.ErrTxDone;
            }
            _done = true;
            try
            {
                _tx.Commit();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Rollback()
        {
            if (_done)
            {
                return Package.ErrTxDone;
            }
            _done = true;
            try
            {
                _tx.Rollback();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("*sql.Rows", "error")]
        public (GoRows?, object?) Query(string query, params object?[] args)
        {
            try
            {
                var cmd = _conn.CreateCommand();
                cmd.Transaction = _tx;
                cmd.CommandText = query;
                var reader = cmd.ExecuteReader();
                return (new GoRows(reader, cmd), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("sql.Result", "error")]
        public (IGoResult?, object?) Exec(string query, params object?[] args)
        {
            try
            {
                using var cmd = _conn.CreateCommand();
                cmd.Transaction = _tx;
                cmd.CommandText = query;
                int affected = cmd.ExecuteNonQuery();
                return (new GoExecResult(affected), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("*sql.Row")]
        public GoRow QueryRow(string query, params object?[] args)
        {
            var (rows, err) = Query(query, args);
            return new GoRow(rows, err);
        }
    }

    // sql.DBStats struct
    [GoType("struct", Name = "DBStats", Package = "database/sql")]
    public class GoDbStats
    {
        [GoField(Name = "MaxOpenConnections")] public long MaxOpenConnections;
        [GoField(Name = "OpenConnections")] public long OpenConnections;
        [GoField(Name = "InUse")] public long InUse;
        [GoField(Name = "Idle")] public long Idle;
        [GoField(Name = "WaitCount")] public long WaitCount;
        [GoField(Name = "WaitDuration")] public long WaitDuration;
        [GoField(Name = "MaxIdleClosed")] public long MaxIdleClosed;
        [GoField(Name = "MaxIdleTimeClosed")] public long MaxIdleTimeClosed;
        [GoField(Name = "MaxLifetimeClosed")] public long MaxLifetimeClosed;
    }

    // Internal Result implementation
    internal class GoExecResult : IGoResult
    {
        private readonly long _rowsAffected;

        public GoExecResult(long rowsAffected)
        {
            _rowsAffected = rowsAffected;
        }

        public (long, object?) LastInsertId() => (0, null);
        public (long, object?) RowsAffected() => (_rowsAffected, null);
    }
}
