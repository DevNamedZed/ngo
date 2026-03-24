using System;
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

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(object? value)
        {
            if (value == null)
            {
                Float64 = 0;
                Valid = false;
                return null;
            }
            Float64 = Convert.ToDouble(value);
            Valid = true;
            return null;
        }
    }
}
