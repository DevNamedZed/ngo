using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "NullFloat64", Package = "database/sql")]
    public struct GoNullFloat64
    {
        [GoField(Name = "Float64")]
        public double Float64;

        [GoField(Name = "Valid")]
        public bool Valid;
    }
}
