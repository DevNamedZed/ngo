using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto
{
    [GoPackage("crypto")]
    public static class Package
    {
        // crypto.Hash type constants
        [GoConst(Type = "crypto.Hash")]
        public const long MD4 = 1;
        [GoConst(Type = "crypto.Hash")]
        public const long MD5 = 2;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA1 = 3;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA224 = 4;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA256 = 5;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA384 = 6;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA512 = 7;
        [GoConst(Type = "crypto.Hash")]
        public const long MD5SHA1 = 8;
        [GoConst(Type = "crypto.Hash")]
        public const long RIPEMD160 = 9;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA3_224 = 10;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA3_256 = 11;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA3_384 = 12;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA3_512 = 13;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA512_224 = 14;
        [GoConst(Type = "crypto.Hash")]
        public const long SHA512_256 = 15;
        [GoConst(Type = "crypto.Hash")]
        public const long BLAKE2s_256 = 16;
        [GoConst(Type = "crypto.Hash")]
        public const long BLAKE2b_256 = 17;
        [GoConst(Type = "crypto.Hash")]
        public const long BLAKE2b_384 = 18;
        [GoConst(Type = "crypto.Hash")]
        public const long BLAKE2b_512 = 19;

        // crypto.RegisterHash(h Hash, f func() hash.Hash)
        [GoFunc]
        public static void RegisterHash([GoParam("crypto.Hash")] long h, [GoParam("func() hash.Hash")] object? f) { }

        // crypto.Signer interface
        [GoType("interface", Name = "Signer", Package = "crypto")]
        public interface ISigner
        {
            [GoMethod]
            [return: GoReturn("crypto.PublicKey")]
            object? Public();

            [GoMethod]
            [return: GoReturn("[]byte", "error")]
            (Slice<byte>, object?) Sign(object? rand, Slice<byte> digest, [GoParam("crypto.SignerOpts")] object? opts);
        }

        // crypto.SignerOpts interface
        [GoType("interface", Name = "SignerOpts", Package = "crypto")]
        public interface ISignerOpts
        {
            [GoMethod]
            [return: GoReturn("crypto.Hash")]
            long HashFunc();
        }

        // crypto.Decrypter interface
        [GoType("interface", Name = "Decrypter", Package = "crypto")]
        public interface IDecrypter
        {
            [GoMethod]
            [return: GoReturn("crypto.PublicKey")]
            object? Public();

            [GoMethod]
            [return: GoReturn("[]byte", "error")]
            (Slice<byte>, object?) Decrypt(object? rand, Slice<byte> msg, [GoParam("crypto.DecrypterOpts")] object? opts);
        }

        // crypto.PublicKey is interface{} (empty interface)
        [GoType("interface", Name = "PublicKey", Package = "crypto")]
        public interface IPublicKey { }

        // crypto.PrivateKey is interface{} (empty interface)
        [GoType("interface", Name = "PrivateKey", Package = "crypto")]
        public interface IPrivateKey { }

        // crypto.DecrypterOpts is interface{} (empty interface)
        [GoType("interface", Name = "DecrypterOpts", Package = "crypto")]
        public interface IDecrypterOpts { }
    }

    // crypto.Hash type (named uint)
    [GoType("named", Name = "Hash", Package = "crypto", Underlying = "uint")]
    public struct GoHash
    {
        public long Value;

        [GoMethod]
        [return: GoReturn("hash.Hash")]
        public object? New()
        {
            return Value switch
            {
                Package.SHA1 => Ngo.Runtime.Crypto.Sha1.Package.New(),
                Package.SHA256 => Ngo.Runtime.Sha256.Package.New(),
                Package.SHA512 => Ngo.Runtime.Crypto.Sha512.Package.New(),
                Package.SHA384 => Ngo.Runtime.Crypto.Sha512.Package.New384(),
                Package.MD5 => Ngo.Runtime.Md5.Package.New(),
                _ => null,
            };
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Size()
        {
            return Value switch
            {
                Package.MD5 => 16,
                Package.SHA1 => 20,
                Package.SHA224 => 28,
                Package.SHA256 => 32,
                Package.SHA384 => 48,
                Package.SHA512 => 64,
                Package.SHA512_224 => 28,
                Package.SHA512_256 => 32,
                _ => 0,
            };
        }

        [GoMethod]
        public bool Available()
        {
            return Value switch
            {
                Package.MD5 or Package.SHA1 or Package.SHA224 or Package.SHA256 or
                Package.SHA384 or Package.SHA512 or Package.SHA512_224 or Package.SHA512_256 => true,
                _ => false,
            };
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long HashFunc() => Value;
    }
}
