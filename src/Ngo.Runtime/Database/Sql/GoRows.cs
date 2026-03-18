using System;
using System.Data.Common;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "Rows", Package = "database/sql")]
    public class GoRows
    {
        private readonly DbDataReader? _reader;
        private readonly DbCommand? _cmd;
        private bool _closed;

        public GoRows() { }

        internal GoRows(DbDataReader reader, DbCommand cmd)
        {
            _reader = reader;
            _cmd = cmd;
        }

        [GoMethod]
        public bool Next()
        {
            if (_reader == null || _closed)
            {
                return false;
            }
            try
            {
                return _reader.Read();
            }
            catch
            {
                return false;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(params object?[] dest)
        {
            if (_reader == null)
            {
                return "sql: Rows are closed";
            }
            try
            {
                for (int i = 0; i < dest.Length && i < _reader.FieldCount; i++)
                {
                    object dbValue = _reader.IsDBNull(i) ? null! : _reader.GetValue(i);
                    ScanInto(dest[i], dbValue);
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close()
        {
            _closed = true;
            try
            {
                _reader?.Close();
                _cmd?.Dispose();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Err() => null;

        [GoMethod]
        [return: GoReturn("[]string", "error")]
        public (Slice<string>, object?) Columns()
        {
            if (_reader == null)
            {
                return (new Slice<string>(Array.Empty<string>()), null);
            }
            try
            {
                var cols = new string[_reader.FieldCount];
                for (int i = 0; i < _reader.FieldCount; i++)
                {
                    cols[i] = _reader.GetName(i);
                }
                return (new Slice<string>(cols), null);
            }
            catch (Exception ex)
            {
                return (new Slice<string>(Array.Empty<string>()), ex.Message);
            }
        }

        private static void ScanInto(object? dest, object? value)
        {
            if (dest == null)
            {
                return;
            }

            // Handle Ptr<T> types (value types)
            var destType = dest.GetType();
            if (destType.IsGenericType && destType.GetGenericTypeDefinition() == typeof(Ptr<>))
            {
                var field = destType.GetField("Value");
                if (field != null)
                {
                    var targetType = field.FieldType;
                    object converted = value == null ? Activator.CreateInstance(targetType)! : Convert.ChangeType(value, targetType);
                    field.SetValue(dest, converted);
                }
                return;
            }

            // Handle GoNullString, GoNullInt64, etc.
            if (dest is GoNullString nullStr)
            {
                nullStr.String = value?.ToString() ?? "";
                nullStr.Valid = value != null;
                return;
            }
            if (dest is GoNullInt64 nullInt)
            {
                nullInt.Int64 = value != null ? Convert.ToInt64(value) : 0;
                nullInt.Valid = value != null;
                return;
            }
            if (dest is GoNullFloat64 nullFloat)
            {
                nullFloat.Float64 = value != null ? Convert.ToDouble(value) : 0;
                nullFloat.Valid = value != null;
                return;
            }
            if (dest is GoNullBool nullBool)
            {
                nullBool.Bool = value != null && Convert.ToBoolean(value);
                nullBool.Valid = value != null;
                return;
            }
        }

        [GoMethod]
        public bool NextResultSet()
        {
            if (_reader == null)
            {
                return false;
            }
            try
            {
                return _reader.NextResult();
            }
            catch
            {
                return false;
            }
        }
    }
}
