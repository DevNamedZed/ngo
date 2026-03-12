using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoPackage("database/sql")]
    public static class Package
    {
        // sql.Scanner interface { Scan(src interface{}) error }
        [GoType("interface", Name = "Scanner", Package = "database/sql")]
        public interface IScanner
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? Scan(object? src);
        }
    }
}
