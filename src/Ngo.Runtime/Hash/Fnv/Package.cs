using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Hash.Fnv
{
    [GoPackage("hash/fnv")]
    public static class Package
    {
        // fnv.New32() hash.Hash32
        [GoFunc]
        [return: GoReturn("hash.Hash32")]
        public static object New32() => throw new System.NotImplementedException();

        // fnv.New32a() hash.Hash32
        [GoFunc]
        [return: GoReturn("hash.Hash32")]
        public static object New32a() => throw new System.NotImplementedException();

        // fnv.New64() hash.Hash64
        [GoFunc]
        [return: GoReturn("hash.Hash64")]
        public static object New64() => throw new System.NotImplementedException();

        // fnv.New64a() hash.Hash64
        [GoFunc]
        [return: GoReturn("hash.Hash64")]
        public static object New64a() => throw new System.NotImplementedException();

        // fnv.New128() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static object New128() => throw new System.NotImplementedException();

        // fnv.New128a() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static object New128a() => throw new System.NotImplementedException();
    }
}
