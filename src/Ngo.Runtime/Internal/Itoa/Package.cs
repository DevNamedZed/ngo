using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Itoa
{
    /// <summary>
    /// internal/itoa — integer to ASCII conversion used by strconv and fmt.
    /// </summary>
    [GoPackage("internal/itoa")]
    public static class Package
    {
        [GoFunc]
        public static string Itoa([GoParam("int")] long val) => val.ToString();

        [GoFunc]
        public static string Uitoa([GoParam("uint")] long val) => ((ulong)val).ToString();

        [GoFunc]
        public static string Uitox([GoParam("uint")] long val) => "0x" + ((ulong)val).ToString("x");
    }
}
