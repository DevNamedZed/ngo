using System;
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

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(object? value)
        {
            if (value == null)
            {
                Int64 = 0;
                Valid = false;
                return null;
            }
            Int64 = Convert.ToInt64(value);
            Valid = true;
            return null;
        }
    }
}
