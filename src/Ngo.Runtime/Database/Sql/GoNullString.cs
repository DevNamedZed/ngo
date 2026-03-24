using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "NullString", Package = "database/sql")]
    public struct GoNullString
    {
        [GoField(Name = "String")]
        public string String;

        [GoField(Name = "Valid")]
        public bool Valid;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(object? value)
        {
            if (value == null)
            {
                String = "";
                Valid = false;
                return null;
            }
            String = value.ToString() ?? "";
            Valid = true;
            return null;
        }
    }
}
