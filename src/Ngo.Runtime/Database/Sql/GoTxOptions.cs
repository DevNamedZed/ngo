using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "TxOptions", Package = "database/sql")]
    public struct GoTxOptions
    {
        [GoField(Name = "Isolation")]
        public long Isolation;

        [GoField(Name = "ReadOnly")]
        public bool ReadOnly;
    }
}
