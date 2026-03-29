using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net
{
    [GoType("named", Name = "IPMask", Package = "net", Underlying = "[]byte")]
    public class GoIPMask
    {
        public Slice<byte> Bytes;

        public GoIPMask() { Bytes = default; }
        public GoIPMask(Slice<byte> bytes) { Bytes = bytes; }

        [GoMethod]
        public string String()
        {
            if (Bytes.Len == 0)
            {
                return "<nil>";
            }
            var hex = new System.Text.StringBuilder(Bytes.Len * 2);
            for (int i = 0; i < Bytes.Len; i++)
            {
                hex.Append(Bytes[i].ToString("x2"));
            }
            return hex.ToString();
        }

        [GoMethod]
        [return: GoReturn("int", "int")]
        public (long, long) Size()
        {
            int ones = 0;
            int bits = Bytes.Len * 8;
            bool seenZero = false;
            for (int i = 0; i < Bytes.Len; i++)
            {
                byte maskByte = Bytes[i];
                if (maskByte == 0xff)
                {
                    if (seenZero)
                    {
                        return (0, 0);
                    }
                    ones += 8;
                }
                else if (maskByte == 0)
                {
                    seenZero = true;
                }
                else
                {
                    if (seenZero)
                    {
                        return (0, 0);
                    }
                    seenZero = true;
                    while ((maskByte & 0x80) != 0)
                    {
                        ones++;
                        maskByte <<= 1;
                    }
                    if ((byte)(maskByte << 1) != 0)
                    {
                        return (0, 0);
                    }
                }
            }
            return (ones, bits);
        }
    }
}
