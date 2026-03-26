using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "Null", Package = "database/sql", TypeParams = "T")]
    public struct GoNullGeneric
    {
        [GoField(Name = "V")]
        public object? V;

        [GoField(Name = "Valid")]
        public bool Valid;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(object? value)
        {
            if (value == null)
            {
                V = default;
                Valid = false;
                return null;
            }
            V = value;
            Valid = true;
            return null;
        }
    }
}
