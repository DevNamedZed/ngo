using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "ColumnType", Package = "database/sql")]
    public class GoColumnType
    {
        [GoMethod]
        public string Name() => "";

        [GoMethod]
        public string DatabaseTypeName() => "";

        [GoMethod]
        [return: GoReturn("reflect.Type")]
        public object? ScanType() => null;

        [GoMethod]
        public (long, bool) Length() => (0, false);

        [GoMethod]
        public (long, long, bool) DecimalSize() => (0, 0, false);

        [GoMethod]
        public (bool, bool) Nullable() => (false, false);
    }
}
