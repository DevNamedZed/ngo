using System.Collections.Generic;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Pem
{
    [GoPackage("encoding/pem")]
    public static class Package
    {
        // pem.Decode(data []byte) (p *Block, rest []byte)
        [GoFunc]
        [return: GoReturn("*pem.Block", "[]byte")]
        public static (GoBlock?, Slice<byte>) Decode(Slice<byte> data) => (null, new Slice<byte>());

        // pem.Encode(out io.Writer, b *Block) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Encode([GoParam("io.Writer")] object? @out, [GoParam("*pem.Block")] GoBlock? b) => null;

        // pem.EncodeToMemory(b *Block) []byte
        [GoFunc]
        [return: GoReturn("[]byte")]
        public static Slice<byte> EncodeToMemory([GoParam("*pem.Block")] GoBlock? b) => new Slice<byte>();
    }

    // pem.Block struct
    [GoType("struct", Name = "Block", Package = "encoding/pem")]
    public class GoBlock
    {
        [GoField(Name = "Type")] public string Type = "";
        [GoField(Name = "Headers")] public Map<string, string> Headers = new Map<string, string>();
        [GoField(Name = "Bytes")] public Slice<byte> Bytes;
    }
}
