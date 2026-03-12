using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "NullInt64", Package = "database/sql")]
    public struct GoNullInt64
    {
        [GoField(Name = "Int64")]
        public long Int64;

        [GoField(Name = "Valid")]
        public bool Valid;
    }
}
