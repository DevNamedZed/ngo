using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "Row", Package = "database/sql")]
    public class GoRow
    {
        private readonly GoRows? _rows;
        private readonly object? _err;
        private bool _scanned;

        public GoRow() { }

        internal GoRow(GoRows? rows, object? err)
        {
            _rows = rows;
            _err = err;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(params object?[] dest)
        {
            if (_err != null)
            {
                return _err;
            }
            if (_rows == null)
            {
                return Package.ErrNoRows;
            }
            if (_scanned)
            {
                return "sql: Row has already been scanned";
            }
            _scanned = true;

            if (!_rows.Next())
            {
                _rows.Close();
                return Package.ErrNoRows;
            }

            var scanErr = _rows.Scan(dest);
            _rows.Close();
            return scanErr;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Err()
        {
            return _err;
        }
    }
}
