using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Rsa
{
    // rsa.PublicKey struct
    [GoType("struct", Name = "PublicKey", Package = "crypto/rsa")]
    public class GoPublicKey
    {
        [GoField(Name = "N")] public object? N; // *big.Int
        [GoField(Name = "E")] public long E;

        [GoMethod]
        [return: GoReturn("int")]
        public long Size() => 0;

        [GoMethod]
        public bool Equal([GoParam("crypto.PublicKey")] object? x) => false;
    }
}
