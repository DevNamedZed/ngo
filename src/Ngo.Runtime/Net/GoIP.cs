using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net
{
    [GoType("named", Name = "IP", Package = "net", Underlying = "[]byte")]
    public class GoIP
    {
        public Slice<byte> Bytes;

        public GoIP() { Bytes = default; }
        public GoIP(Slice<byte> bytes) { Bytes = bytes; }

        [GoMethod]
        public string String()
        {
            if (Bytes.Len == 0)
            {
                return "<nil>";
            }
            if (Bytes.Len == 4)
            {
                return $"{Bytes[0]}.{Bytes[1]}.{Bytes[2]}.{Bytes[3]}";
            }
            if (Bytes.Len == 16)
            {
                // Check for IPv4-mapped IPv6
                bool isV4Mapped = true;
                for (int i = 0; i < 10; i++)
                {
                    if (Bytes[i] != 0) { isV4Mapped = false; break; }
                }
                if (isV4Mapped && Bytes[10] == 0xff && Bytes[11] == 0xff)
                {
                    return $"::ffff:{Bytes[12]}.{Bytes[13]}.{Bytes[14]}.{Bytes[15]}";
                }
                var parts = new string[8];
                for (int i = 0; i < 8; i++)
                {
                    parts[i] = ((Bytes[i * 2] << 8) | Bytes[i * 2 + 1]).ToString("x");
                }
                return string.Join(":", parts);
            }
            return "?";
        }

        [GoMethod]
        [return: GoReturn("IP")]
        public object? To4()
        {
            if (Bytes.Len == 4)
            {
                return new GoIP(Bytes);
            }
            if (Bytes.Len == 16)
            {
                // Check IPv4-mapped IPv6: ::ffff:x.x.x.x
                bool isV4Mapped = true;
                for (int i = 0; i < 10; i++)
                {
                    if (Bytes[i] != 0) { isV4Mapped = false; break; }
                }
                if (isV4Mapped && Bytes[10] == 0xff && Bytes[11] == 0xff)
                {
                    return new GoIP(new Slice<byte>(new byte[] { Bytes[12], Bytes[13], Bytes[14], Bytes[15] }));
                }
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("IP")]
        public object? To16()
        {
            if (Bytes.Len == 4)
            {
                var result = new byte[16];
                result[10] = 0xff;
                result[11] = 0xff;
                result[12] = Bytes[0];
                result[13] = Bytes[1];
                result[14] = Bytes[2];
                result[15] = Bytes[3];
                return new GoIP(new Slice<byte>(result));
            }
            if (Bytes.Len == 16)
            {
                return new GoIP(Bytes);
            }
            return null;
        }

        [GoMethod]
        public bool Equal(object? other)
        {
            Slice<byte> otherBytes = default;
            if (other is GoIP otherIP)
            {
                otherBytes = otherIP.Bytes;
            }
            else if (other is Slice<byte> otherSlice)
            {
                otherBytes = otherSlice;
            }
            else
            {
                return false;
            }

            if (Bytes.Len == otherBytes.Len)
            {
                for (int i = 0; i < Bytes.Len; i++)
                {
                    if (Bytes[i] != otherBytes[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        [GoMethod]
        public bool IsLoopback()
        {
            if (Bytes.Len == 4)
            {
                return Bytes[0] == 127;
            }
            if (Bytes.Len == 16)
            {
                for (int i = 0; i < 15; i++)
                {
                    if (Bytes[i] != 0)
                    {
                        return false;
                    }
                }
                return Bytes[15] == 1;
            }
            return false;
        }

        [GoMethod]
        public bool IsUnspecified()
        {
            if (Bytes.Len == 4)
            {
                return Bytes[0] == 0 && Bytes[1] == 0 && Bytes[2] == 0 && Bytes[3] == 0;
            }
            if (Bytes.Len == 16)
            {
                for (int i = 0; i < 16; i++)
                {
                    if (Bytes[i] != 0)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        [GoMethod]
        public bool IsGlobalUnicast()
        {
            return Bytes.Len > 0 && !IsUnspecified() && !IsLoopback() && !IsMulticast() && !IsLinkLocalUnicast();
        }

        [GoMethod]
        public bool IsLinkLocalUnicast()
        {
            if (Bytes.Len == 4)
            {
                return Bytes[0] == 169 && Bytes[1] == 254;
            }
            if (Bytes.Len == 16)
            {
                return Bytes[0] == 0xfe && (Bytes[1] & 0xc0) == 0x80;
            }
            return false;
        }

        [GoMethod]
        public bool IsLinkLocalMulticast()
        {
            if (Bytes.Len == 4)
            {
                return Bytes[0] == 224 && Bytes[1] == 0 && Bytes[2] == 0;
            }
            if (Bytes.Len == 16)
            {
                return Bytes[0] == 0xff && (Bytes[1] & 0x0f) == 0x02;
            }
            return false;
        }

        [GoMethod]
        public bool IsInterfaceLocalMulticast()
        {
            if (Bytes.Len == 16)
            {
                return Bytes[0] == 0xff && (Bytes[1] & 0x0f) == 0x01;
            }
            return false;
        }

        [GoMethod]
        public bool IsMulticast()
        {
            if (Bytes.Len == 4)
            {
                return (Bytes[0] & 0xf0) == 0xe0;
            }
            if (Bytes.Len == 16)
            {
                return Bytes[0] == 0xff;
            }
            return false;
        }

        [GoMethod]
        [return: GoReturn("IP")]
        public object? Mask(object? mask)
        {
            if (mask is Slice<byte> maskBytes && maskBytes.Len == Bytes.Len)
            {
                var result = new byte[Bytes.Len];
                for (int i = 0; i < Bytes.Len; i++)
                {
                    result[i] = (byte)(Bytes[i] & maskBytes[i]);
                }
                return new GoIP(new Slice<byte>(result));
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("IPMask")]
        public object? DefaultMask()
        {
            if (Bytes.Len != 4)
            {
                return null;
            }
            byte first = Bytes[0];
            if (first < 128)
            {
                return new Slice<byte>(new byte[] { 255, 0, 0, 0 });
            }
            if (first < 192)
            {
                return new Slice<byte>(new byte[] { 255, 255, 0, 0 });
            }
            return new Slice<byte>(new byte[] { 255, 255, 255, 0 });
        }

        [GoMethod]
        [return: GoReturn("string")]
        public string MarshalText() => String();
    }
}
