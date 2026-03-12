using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding
{
    [GoPackage("encoding")]
    public static class Package
    {
        // encoding.TextMarshaler interface
        [GoType("interface", Name = "TextMarshaler", Package = "encoding")]
        public interface ITextMarshaler
        {
            [GoMethod]
            [return: GoReturn("[]byte", "error")]
            (Slice<byte>, object?) MarshalText();
        }

        // encoding.TextUnmarshaler interface
        [GoType("interface", Name = "TextUnmarshaler", Package = "encoding")]
        public interface ITextUnmarshaler
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? UnmarshalText(Slice<byte> text);
        }

        // encoding.BinaryMarshaler interface
        [GoType("interface", Name = "BinaryMarshaler", Package = "encoding")]
        public interface IBinaryMarshaler
        {
            [GoMethod]
            [return: GoReturn("[]byte", "error")]
            (Slice<byte>, object?) MarshalBinary();
        }

        // encoding.BinaryUnmarshaler interface
        [GoType("interface", Name = "BinaryUnmarshaler", Package = "encoding")]
        public interface IBinaryUnmarshaler
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? UnmarshalBinary(Slice<byte> data);
        }
    }
}
