using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("interface", Name = "Result", Package = "database/sql")]
    public interface IGoResult
    {
        [GoMethod]
        [return: GoReturn("int64", "error")]
        (long, object?) LastInsertId();

        [GoMethod]
        [return: GoReturn("int64", "error")]
        (long, object?) RowsAffected();
    }
}
