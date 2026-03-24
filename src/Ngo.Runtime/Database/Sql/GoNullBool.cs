using System;
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

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(object? value)
        {
            if (value == null)
            {
                Bool = false;
                Valid = false;
                return null;
            }
            Bool = Convert.ToBoolean(value);
            Valid = true;
            return null;
        }
    }
}
