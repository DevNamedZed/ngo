using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql.Driver
{
    [GoPackage("database/sql/driver")]
    public static class Package
    {
        // Error variables
        [GoVar] public static readonly object? ErrSkip = "driver: skip fast-path; continue as if unimplemented";
        [GoVar] public static readonly object? ErrBadConn = "driver: bad connection";
        [GoVar] public static readonly object? ErrRemoveArgument = "driver: remove argument from query";

        // ResultNoRows variable
        [GoVar(Type = "driver.noRows")] public static readonly object? ResultNoRows = new GoNoRows();

        // IsValue(v any) bool
        [GoFunc]
        public static bool IsValue(object? v) => true;

        // IsScanValue(v any) bool
        [GoFunc]
        public static bool IsScanValue(object? v) => true;

        // driver.Value is type Value = any
        [GoType("interface", Name = "Value", Package = "database/sql/driver")]
        public interface IValue
        {
        }

        // driver.Valuer interface
        [GoType("interface", Name = "Valuer", Package = "database/sql/driver")]
        public interface IValuer
        {
            [GoMethod]
            [return: GoReturn("driver.Value", "error")]
            (object?, object?) Value();
        }

        // driver.ValueConverter interface
        [GoType("interface", Name = "ValueConverter", Package = "database/sql/driver")]
        public interface IValueConverter
        {
            [GoMethod]
            [return: GoReturn("driver.Value", "error")]
            (object?, object?) ConvertValue(object? v);
        }

        // driver.Driver interface
        [GoType("interface", Name = "Driver", Package = "database/sql/driver")]
        public interface IDriver
        {
            [GoMethod]
            [return: GoReturn("driver.Conn", "error")]
            (object?, object?) Open(string name);
        }

        // driver.DriverContext interface
        [GoType("interface", Name = "DriverContext", Package = "database/sql/driver")]
        public interface IDriverContext
        {
            [GoMethod]
            [return: GoReturn("driver.Connector", "error")]
            (object?, object?) OpenConnector(string name);
        }

        // driver.Connector interface
        [GoType("interface", Name = "Connector", Package = "database/sql/driver")]
        public interface IConnector
        {
            [GoMethod]
            [return: GoReturn("driver.Conn", "error")]
            (object?, object?) Connect([GoParam("context.Context")] object? ctx);

            [GoMethod]
            [return: GoReturn("driver.Driver")]
            object? Driver();
        }

        // driver.Pinger interface
        [GoType("interface", Name = "Pinger", Package = "database/sql/driver")]
        public interface IPinger
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? Ping([GoParam("context.Context")] object? ctx);
        }

        // driver.Execer interface (deprecated)
        [GoType("interface", Name = "Execer", Package = "database/sql/driver")]
        public interface IExecer
        {
            [GoMethod]
            [return: GoReturn("driver.Result", "error")]
            (object?, object?) Exec(string query, Slice<object?> args);
        }

        // driver.ExecerContext interface
        [GoType("interface", Name = "ExecerContext", Package = "database/sql/driver")]
        public interface IExecerContext
        {
            [GoMethod]
            [return: GoReturn("driver.Result", "error")]
            (object?, object?) ExecContext([GoParam("context.Context")] object? ctx, string query, Slice<GoNamedValue> args);
        }

        // driver.Queryer interface (deprecated)
        [GoType("interface", Name = "Queryer", Package = "database/sql/driver")]
        public interface IQueryer
        {
            [GoMethod]
            [return: GoReturn("driver.Rows", "error")]
            (object?, object?) Query(string query, Slice<object?> args);
        }

        // driver.QueryerContext interface
        [GoType("interface", Name = "QueryerContext", Package = "database/sql/driver")]
        public interface IQueryerContext
        {
            [GoMethod]
            [return: GoReturn("driver.Rows", "error")]
            (object?, object?) QueryContext([GoParam("context.Context")] object? ctx, string query, Slice<GoNamedValue> args);
        }

        // driver.Conn interface
        [GoType("interface", Name = "Conn", Package = "database/sql/driver")]
        public interface IConn
        {
            [GoMethod]
            [return: GoReturn("driver.Stmt", "error")]
            (object?, object?) Prepare(string query);

            [GoMethod]
            [return: GoReturn("error")]
            object? Close();

            [GoMethod]
            [return: GoReturn("driver.Tx", "error")]
            (object?, object?) Begin();
        }

        // driver.ConnPrepareContext interface
        [GoType("interface", Name = "ConnPrepareContext", Package = "database/sql/driver")]
        public interface IConnPrepareContext
        {
            [GoMethod]
            [return: GoReturn("driver.Stmt", "error")]
            (object?, object?) PrepareContext([GoParam("context.Context")] object? ctx, string query);
        }

        // driver.ConnBeginTx interface
        [GoType("interface", Name = "ConnBeginTx", Package = "database/sql/driver")]
        public interface IConnBeginTx
        {
            [GoMethod]
            [return: GoReturn("driver.Tx", "error")]
            (object?, object?) BeginTx([GoParam("context.Context")] object? ctx, [GoParam("driver.TxOptions")] GoTxOptions opts);
        }

        // driver.SessionResetter interface
        [GoType("interface", Name = "SessionResetter", Package = "database/sql/driver")]
        public interface ISessionResetter
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? ResetSession([GoParam("context.Context")] object? ctx);
        }

        // driver.Validator interface
        [GoType("interface", Name = "Validator", Package = "database/sql/driver")]
        public interface IValidator
        {
            [GoMethod]
            bool IsValid();
        }

        // driver.Result interface
        [GoType("interface", Name = "Result", Package = "database/sql/driver")]
        public interface IResult
        {
            [GoMethod]
            [return: GoReturn("int64", "error")]
            (long, object?) LastInsertId();

            [GoMethod]
            [return: GoReturn("int64", "error")]
            (long, object?) RowsAffected();
        }

        // driver.Stmt interface
        [GoType("interface", Name = "Stmt", Package = "database/sql/driver")]
        public interface IStmt
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? Close();

            [GoMethod]
            [return: GoReturn("int")]
            long NumInput();

            [GoMethod]
            [return: GoReturn("driver.Result", "error")]
            (object?, object?) Exec(Slice<object?> args);

            [GoMethod]
            [return: GoReturn("driver.Rows", "error")]
            (object?, object?) Query(Slice<object?> args);
        }

        // driver.StmtExecContext interface
        [GoType("interface", Name = "StmtExecContext", Package = "database/sql/driver")]
        public interface IStmtExecContext
        {
            [GoMethod]
            [return: GoReturn("driver.Result", "error")]
            (object?, object?) ExecContext([GoParam("context.Context")] object? ctx, Slice<GoNamedValue> args);
        }

        // driver.StmtQueryContext interface
        [GoType("interface", Name = "StmtQueryContext", Package = "database/sql/driver")]
        public interface IStmtQueryContext
        {
            [GoMethod]
            [return: GoReturn("driver.Rows", "error")]
            (object?, object?) QueryContext([GoParam("context.Context")] object? ctx, Slice<GoNamedValue> args);
        }

        // driver.NamedValueChecker interface
        [GoType("interface", Name = "NamedValueChecker", Package = "database/sql/driver")]
        public interface INamedValueChecker
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? CheckNamedValue([GoParam("*driver.NamedValue")] GoNamedValue nv);
        }

        // driver.ColumnConverter interface (deprecated)
        [GoType("interface", Name = "ColumnConverter", Package = "database/sql/driver")]
        public interface IColumnConverter
        {
            [GoMethod]
            [return: GoReturn("driver.ValueConverter")]
            object? ColumnConverter([GoParam("int")] long idx);
        }

        // driver.Rows interface
        [GoType("interface", Name = "Rows", Package = "database/sql/driver")]
        public interface IRows
        {
            [GoMethod]
            [return: GoReturn("[]string")]
            Slice<string> Columns();

            [GoMethod]
            [return: GoReturn("error")]
            object? Close();

            [GoMethod]
            [return: GoReturn("error")]
            object? Next(Slice<object?> dest);
        }

        // driver.RowsNextResultSet interface
        [GoType("interface", Name = "RowsNextResultSet", Package = "database/sql/driver")]
        public interface IRowsNextResultSet
        {
            [GoMethod]
            bool HasNextResultSet();

            [GoMethod]
            [return: GoReturn("error")]
            object? NextResultSet();
        }

        // driver.RowsColumnTypeScanType interface
        [GoType("interface", Name = "RowsColumnTypeScanType", Package = "database/sql/driver")]
        public interface IRowsColumnTypeScanType
        {
            [GoMethod]
            [return: GoReturn("reflect.Type")]
            object? ColumnTypeScanType([GoParam("int")] long index);
        }

        // driver.RowsColumnTypeDatabaseTypeName interface
        [GoType("interface", Name = "RowsColumnTypeDatabaseTypeName", Package = "database/sql/driver")]
        public interface IRowsColumnTypeDatabaseTypeName
        {
            [GoMethod]
            string ColumnTypeDatabaseTypeName([GoParam("int")] long index);
        }

        // driver.RowsColumnTypeLength interface
        [GoType("interface", Name = "RowsColumnTypeLength", Package = "database/sql/driver")]
        public interface IRowsColumnTypeLength
        {
            [GoMethod]
            [return: GoReturn("int64", "bool")]
            (long, bool) ColumnTypeLength([GoParam("int")] long index);
        }

        // driver.RowsColumnTypeNullable interface
        [GoType("interface", Name = "RowsColumnTypeNullable", Package = "database/sql/driver")]
        public interface IRowsColumnTypeNullable
        {
            [GoMethod]
            [return: GoReturn("bool", "bool")]
            (bool, bool) ColumnTypeNullable([GoParam("int")] long index);
        }

        // driver.RowsColumnTypePrecisionScale interface
        [GoType("interface", Name = "RowsColumnTypePrecisionScale", Package = "database/sql/driver")]
        public interface IRowsColumnTypePrecisionScale
        {
            [GoMethod]
            [return: GoReturn("int64", "int64", "bool")]
            (long, long, bool) ColumnTypePrecisionScale([GoParam("int")] long index);
        }

        // driver.Tx interface
        [GoType("interface", Name = "Tx", Package = "database/sql/driver")]
        public interface ITx
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? Commit();

            [GoMethod]
            [return: GoReturn("error")]
            object? Rollback();
        }

        // Converter variables
        [GoVar(Type = "driver.boolType")] public static readonly object? Bool = new GoBoolType();
        [GoVar(Type = "driver.int32Type")] public static readonly object? Int32 = new GoInt32Type();
        [GoVar(Type = "driver.stringType")] public static readonly object? String = new GoStringType();
        [GoVar(Type = "driver.defaultConverter")] public static readonly object? DefaultParameterConverter = new GoDefaultConverter();
    }

    // driver.NamedValue struct
    [GoType("struct", Name = "NamedValue", Package = "database/sql/driver")]
    public class GoNamedValue
    {
        [GoField(Name = "Name")] public string Name = "";
        [GoField(Name = "Ordinal")] public long Ordinal;
        [GoField(Name = "Value")] public object? Value;
    }

    // driver.TxOptions struct
    [GoType("struct", Name = "TxOptions", Package = "database/sql/driver")]
    public class GoTxOptions
    {
        [GoField(Name = "Isolation")] public long Isolation;
        [GoField(Name = "ReadOnly")] public bool ReadOnly;
    }

    // driver.IsolationLevel named type
    [GoType("named", Name = "IsolationLevel", Package = "database/sql/driver", Underlying = "int")]
    public struct GoIsolationLevel { public long Value; }

    // driver.RowsAffected named type
    [GoType("named", Name = "RowsAffected", Package = "database/sql/driver", Underlying = "int64")]
    public class GoRowsAffected
    {
        internal long _value;

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) LastInsertId() => (0, "LastInsertId is not supported by this driver");

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) RowsAffected() => (_value, null);
    }

    // driver.noRows struct (internal but exported as ResultNoRows)
    [GoType("struct", Name = "noRows", Package = "database/sql/driver")]
    public class GoNoRows
    {
        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) LastInsertId() => (0, "no LastInsertId available");

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) RowsAffected() => (0, "no RowsAffected available");
    }

    // driver.Null struct
    [GoType("struct", Name = "Null", Package = "database/sql/driver")]
    public class GoNull
    {
        [GoField(Name = "Converter", Type = "driver.ValueConverter")] public object? Converter;

        [GoMethod]
        [return: GoReturn("driver.Value", "error")]
        public (object?, object?) ConvertValue(object? v) => (v, null);
    }

    // driver.NotNull struct
    [GoType("struct", Name = "NotNull", Package = "database/sql/driver")]
    public class GoNotNull
    {
        [GoField(Name = "Converter", Type = "driver.ValueConverter")] public object? Converter;

        [GoMethod]
        [return: GoReturn("driver.Value", "error")]
        public (object?, object?) ConvertValue(object? v)
        {
            if (v == null) return (null, (object?)"nil value not allowed");
            return (v, null);
        }
    }

    // Internal converter types for Bool, Int32, String variables
    [GoType("struct", Name = "boolType", Package = "database/sql/driver")]
    public class GoBoolType
    {
        [GoMethod]
        [return: GoReturn("driver.Value", "error")]
        public (object?, object?) ConvertValue(object? v) => (v, null);
    }

    [GoType("struct", Name = "int32Type", Package = "database/sql/driver")]
    public class GoInt32Type
    {
        [GoMethod]
        [return: GoReturn("driver.Value", "error")]
        public (object?, object?) ConvertValue(object? v) => (v, null);
    }

    [GoType("struct", Name = "stringType", Package = "database/sql/driver")]
    public class GoStringType
    {
        [GoMethod]
        [return: GoReturn("driver.Value", "error")]
        public (object?, object?) ConvertValue(object? v) => (v, null);
    }

    [GoType("struct", Name = "defaultConverter", Package = "database/sql/driver")]
    public class GoDefaultConverter
    {
        [GoMethod]
        [return: GoReturn("driver.Value", "error")]
        public (object?, object?) ConvertValue(object? v) => (v, null);
    }
}
