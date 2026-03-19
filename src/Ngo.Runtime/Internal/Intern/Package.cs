using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Intern
{
    [GoPackage("internal/intern")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*Value")]
        public static GoValue Get([GoParam("interface{}")] object? v) => new GoValue { Inner = v };

        [GoFunc]
        [return: GoReturn("*Value")]
        public static GoValue GetByString(string s) => new GoValue { Inner = s };
    }

    [GoType("struct", Name = "Value", Package = "internal/intern")]
    public class GoValue
    {
        public object? Inner;

        [GoMethod]
        [return: GoReturn("interface{}")]
        public object? Get() => Inner;
    }
}
