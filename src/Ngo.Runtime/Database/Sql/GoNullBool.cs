using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "NullBool", Package = "database/sql")]
    public struct GoNullBool
    {
        [GoField(Name = "Bool")]
        public bool Bool;

        [GoField(Name = "Valid")]
        public bool Valid;
    }
}
