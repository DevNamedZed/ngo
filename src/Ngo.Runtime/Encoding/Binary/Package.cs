using System;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Encoding.Binary
{
    [GoPackage("encoding/binary")]
    public static class Package
    {
        // ByteOrder interface
        [GoType("interface", Name = "ByteOrder", Package = "encoding/binary")]
        public interface IByteOrder
        {
            [GoMethod]
            ushort Uint16(Slice<byte> b);
            [GoMethod]
            uint Uint32(Slice<byte> b);
            [GoMethod]
            ulong Uint64(Slice<byte> b);
            [GoMethod]
            void PutUint16(Slice<byte> b, ushort v);
            [GoMethod]
            void PutUint32(Slice<byte> b, uint v);
            [GoMethod]
            void PutUint64(Slice<byte> b, ulong v);
            [GoMethod]
            string String();
        }

        // BigEndian and LittleEndian vars
        [GoVar(Type = "encoding/binary.bigEndian")]
        public static readonly GoBigEndian BigEndian = new GoBigEndian();

        [GoVar(Type = "encoding/binary.littleEndian")]
        public static readonly GoLittleEndian LittleEndian = new GoLittleEndian();

        [GoVar(Type = "encoding/binary.nativeEndian")]
        public static readonly GoNativeEndian NativeEndian = new GoNativeEndian();

        // binary.Read(r io.Reader, order ByteOrder, data interface{}) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Read(object? r, object? order, object? data)
        {
            if (r is not IGoReader reader)
            {
                return "binary.Read: invalid reader";
            }
            if (order is not IByteOrder byteOrder)
            {
                return "binary.Read: invalid byte order";
            }
            if (data == null)
            {
                return "binary.Read: invalid data";
            }

            var type = data.GetType();

            // Handle primitive pointer types (Ptr<T>)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Ptr<>))
            {
                var innerType = type.GetGenericArguments()[0];
                int size = SizeOfType(innerType);
                if (size <= 0)
                {
                    return "binary.Read: unsupported type";
                }

                var buf = new byte[size];
                var slice = new Slice<byte>(buf);
                var (n, err) = reader.Read(slice);
                if (err != null && n < size)
                {
                    return err;
                }

                object value = DecodeValue(buf, innerType, byteOrder);
                var field = type.GetField("Value");
                if (field != null)
                {
                    field.SetValue(data, value);
                }
                return null;
            }

            // Handle Slice<byte>
            if (data is Slice<byte> sliceData)
            {
                var buf = new byte[sliceData.Len];
                var readSlice = new Slice<byte>(buf);
                var (n, err) = reader.Read(readSlice);
                if (err != null && n < sliceData.Len)
                {
                    return err;
                }
                for (int i = 0; i < sliceData.Len; i++)
                {
                    sliceData[i] = buf[i];
                }
                return null;
            }

            return "binary.Read: unsupported type";
        }

        // binary.Write(w io.Writer, order ByteOrder, data interface{}) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Write(object? w, object? order, object? data)
        {
            if (w is not IGoWriter writer)
            {
                return "binary.Write: invalid writer";
            }
            if (order is not IByteOrder byteOrder)
            {
                return "binary.Write: invalid byte order";
            }
            if (data == null)
            {
                return "binary.Write: invalid data";
            }

            byte[]? encoded = EncodeValue(data, byteOrder);
            if (encoded == null)
            {
                return "binary.Write: unsupported type";
            }

            writer.Write(new Slice<byte>(encoded));
            return null;
        }

        // binary.Size(v interface{}) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long Size(object? v)
        {
            if (v == null)
            {
                return -1;
            }

            var type = v.GetType();

            if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte))
            {
                return 1;
            }
            if (type == typeof(short) || type == typeof(ushort))
            {
                return 2;
            }
            if (type == typeof(int) || type == typeof(uint) || type == typeof(float))
            {
                return 4;
            }
            if (type == typeof(long) || type == typeof(ulong) || type == typeof(double))
            {
                return 8;
            }

            if (v is Slice<byte> sliceBytes)
            {
                return sliceBytes.Len;
            }

            return -1;
        }

        // binary.PutVarint(buf []byte, x int64) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long PutVarint(Slice<byte> buf, long x)
        {
            ulong ux = (ulong)x << 1;
            if (x < 0)
            {
                ux = ~ux;
            }
            return PutUvarint(buf, ux);
        }

        // binary.PutUvarint(buf []byte, x uint64) int
        [GoFunc]
        [return: GoReturn("int")]
        public static long PutUvarint(Slice<byte> buf, ulong x)
        {
            int i = 0;
            while (x >= 0x80)
            {
                buf[i] = (byte)(x | 0x80);
                x >>= 7;
                i++;
            }
            buf[i] = (byte)x;
            return i + 1;
        }

        // binary.Varint(buf []byte) (int64, int)
        [GoFunc]
        [return: GoReturn("int64", "int")]
        public static (long, long) Varint(Slice<byte> buf)
        {
            var (ux, n) = Uvarint(buf);
            long x = (long)(ux >> 1);
            if ((ux & 1) != 0)
            {
                x = ~x;
            }
            return (x, n);
        }

        // binary.Uvarint(buf []byte) (uint64, int)
        [GoFunc]
        [return: GoReturn("uint64", "int")]
        public static (ulong, long) Uvarint(Slice<byte> buf)
        {
            ulong x = 0;
            int s = 0;
            for (int i = 0; i < buf.Len; i++)
            {
                byte b = buf[i];
                if (i == MaxVarintLen64)
                {
                    // Overflow
                    return (0, -(i + 1));
                }
                if (b < 0x80)
                {
                    if (i == MaxVarintLen64 - 1 && b > 1)
                    {
                        return (0, -(i + 1));
                    }
                    return (x | ((ulong)b << s), i + 1);
                }
                x |= (ulong)(b & 0x7f) << s;
                s += 7;
            }
            return (0, 0);
        }

        // binary.ReadUvarint(r io.ByteReader) (uint64, error)
        [GoFunc]
        [return: GoReturn("uint64", "error")]
        public static (ulong, object?) ReadUvarint([GoParam("io.ByteReader")] object? r)
        {
            if (r == null)
            {
                return (0, "binary.ReadUvarint: nil reader");
            }

            // Try to call ReadByte via reflection
            var method = r.GetType().GetMethod("ReadByte");
            if (method == null)
            {
                return (0, "binary.ReadUvarint: reader does not implement ByteReader");
            }

            ulong x = 0;
            int s = 0;
            for (int i = 0; i < (int)MaxVarintLen64; i++)
            {
                var result = method.Invoke(r, null);
                if (result is ValueTuple<byte, object?> tuple)
                {
                    byte b = tuple.Item1;
                    object? err = tuple.Item2;
                    if (err != null)
                    {
                        return (x, err);
                    }
                    if (b < 0x80)
                    {
                        if (i == (int)MaxVarintLen64 - 1 && b > 1)
                        {
                            return (x, "binary: varint overflows a 64-bit integer");
                        }
                        return (x | ((ulong)b << s), null);
                    }
                    x |= (ulong)(b & 0x7f) << s;
                    s += 7;
                }
                else
                {
                    return (0, "binary.ReadUvarint: unexpected return type");
                }
            }
            return (x, "binary: varint overflows a 64-bit integer");
        }

        // binary.ReadVarint(r io.ByteReader) (int64, error)
        [GoFunc]
        [return: GoReturn("int64", "error")]
        public static (long, object?) ReadVarint([GoParam("io.ByteReader")] object? r)
        {
            var (ux, err) = ReadUvarint(r);
            long x = (long)(ux >> 1);
            if ((ux & 1) != 0)
            {
                x = ~x;
            }
            return (x, err);
        }

        // Constants
        [GoConst(Type = "int")]
        public const long MaxVarintLen16 = 3;

        [GoConst(Type = "int")]
        public const long MaxVarintLen32 = 5;

        [GoConst(Type = "int")]
        public const long MaxVarintLen64 = 10;

        // AppendByteOrder interface (Go 1.19+)
        [GoType("interface", Name = "AppendByteOrder", Package = "encoding/binary")]
        public interface IAppendByteOrder
        {
            [GoMethod]
            Slice<byte> AppendUint16(Slice<byte> b, ushort v);
            [GoMethod]
            Slice<byte> AppendUint32(Slice<byte> b, uint v);
            [GoMethod]
            Slice<byte> AppendUint64(Slice<byte> b, ulong v);
            [GoMethod]
            string String();
        }

        // Helper: get byte size of a primitive type
        private static int SizeOfType(Type t)
        {
            if (t == typeof(bool) || t == typeof(byte) || t == typeof(sbyte))
            {
                return 1;
            }
            if (t == typeof(short) || t == typeof(ushort))
            {
                return 2;
            }
            if (t == typeof(int) || t == typeof(uint) || t == typeof(float))
            {
                return 4;
            }
            if (t == typeof(long) || t == typeof(ulong) || t == typeof(double))
            {
                return 8;
            }
            return -1;
        }

        // Helper: decode bytes to a primitive value using byte order
        private static object DecodeValue(byte[] buf, Type t, IByteOrder order)
        {
            var slice = new Slice<byte>(buf);
            if (t == typeof(byte) || t == typeof(sbyte) || t == typeof(bool))
            {
                if (t == typeof(bool))
                {
                    return buf[0] != 0;
                }
                return buf[0];
            }
            if (t == typeof(short))
            {
                return (short)order.Uint16(slice);
            }
            if (t == typeof(ushort))
            {
                return order.Uint16(slice);
            }
            if (t == typeof(int))
            {
                return (int)order.Uint32(slice);
            }
            if (t == typeof(uint))
            {
                return order.Uint32(slice);
            }
            if (t == typeof(long))
            {
                return (long)order.Uint64(slice);
            }
            if (t == typeof(ulong))
            {
                return order.Uint64(slice);
            }
            if (t == typeof(float))
            {
                uint bits = order.Uint32(slice);
                return BitConverter.Int32BitsToSingle((int)bits);
            }
            if (t == typeof(double))
            {
                ulong bits = order.Uint64(slice);
                return BitConverter.Int64BitsToDouble((long)bits);
            }
            return buf[0];
        }

        // Helper: encode a value to bytes using byte order
        private static byte[]? EncodeValue(object data, IByteOrder order)
        {
            if (data is byte b)
            {
                return new byte[] { b };
            }
            if (data is bool boolVal)
            {
                return new byte[] { boolVal ? (byte)1 : (byte)0 };
            }
            if (data is short s)
            {
                var buf = new byte[2];
                var slice = new Slice<byte>(buf);
                order.PutUint16(slice, (ushort)s);
                return buf;
            }
            if (data is ushort us)
            {
                var buf = new byte[2];
                var slice = new Slice<byte>(buf);
                order.PutUint16(slice, us);
                return buf;
            }
            if (data is int i)
            {
                var buf = new byte[4];
                var slice = new Slice<byte>(buf);
                order.PutUint32(slice, (uint)i);
                return buf;
            }
            if (data is uint ui)
            {
                var buf = new byte[4];
                var slice = new Slice<byte>(buf);
                order.PutUint32(slice, ui);
                return buf;
            }
            if (data is long l)
            {
                var buf = new byte[8];
                var slice = new Slice<byte>(buf);
                order.PutUint64(slice, (ulong)l);
                return buf;
            }
            if (data is ulong ul)
            {
                var buf = new byte[8];
                var slice = new Slice<byte>(buf);
                order.PutUint64(slice, ul);
                return buf;
            }
            if (data is float f)
            {
                var buf = new byte[4];
                var slice = new Slice<byte>(buf);
                order.PutUint32(slice, (uint)BitConverter.SingleToInt32Bits(f));
                return buf;
            }
            if (data is double d)
            {
                var buf = new byte[8];
                var slice = new Slice<byte>(buf);
                order.PutUint64(slice, (ulong)BitConverter.DoubleToInt64Bits(d));
                return buf;
            }
            if (data is Slice<byte> sliceBytes)
            {
                var buf = new byte[sliceBytes.Len];
                for (int idx = 0; idx < sliceBytes.Len; idx++)
                {
                    buf[idx] = sliceBytes[idx];
                }
                return buf;
            }
            return null;
        }
    }

    [GoType("struct", Name = "bigEndian", Package = "encoding/binary")]
    public class GoBigEndian : Encoding.Binary.Package.IByteOrder, Encoding.Binary.Package.IAppendByteOrder
    {
        [GoMethod]
        public ushort Uint16(Slice<byte> b)
        {
            return (ushort)((b[0] << 8) | b[1]);
        }

        [GoMethod]
        public uint Uint32(Slice<byte> b)
        {
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        }

        [GoMethod]
        public ulong Uint64(Slice<byte> b)
        {
            return ((ulong)b[0] << 56) | ((ulong)b[1] << 48) | ((ulong)b[2] << 40) | ((ulong)b[3] << 32) |
                   ((ulong)b[4] << 24) | ((ulong)b[5] << 16) | ((ulong)b[6] << 8) | b[7];
        }

        [GoMethod]
        public void PutUint16(Slice<byte> b, ushort v)
        {
            b[0] = (byte)(v >> 8);
            b[1] = (byte)v;
        }

        [GoMethod]
        public void PutUint32(Slice<byte> b, uint v)
        {
            b[0] = (byte)(v >> 24);
            b[1] = (byte)(v >> 16);
            b[2] = (byte)(v >> 8);
            b[3] = (byte)v;
        }

        [GoMethod]
        public void PutUint64(Slice<byte> b, ulong v)
        {
            b[0] = (byte)(v >> 56);
            b[1] = (byte)(v >> 48);
            b[2] = (byte)(v >> 40);
            b[3] = (byte)(v >> 32);
            b[4] = (byte)(v >> 24);
            b[5] = (byte)(v >> 16);
            b[6] = (byte)(v >> 8);
            b[7] = (byte)v;
        }

        [GoMethod]
        public string String() => "BigEndian";

        [GoMethod]
        public Slice<byte> AppendUint16(Slice<byte> b, ushort v)
        {
            return Slice<byte>.Append(b, (byte)(v >> 8), (byte)v);
        }

        [GoMethod]
        public Slice<byte> AppendUint32(Slice<byte> b, uint v)
        {
            return Slice<byte>.Append(b, (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }

        [GoMethod]
        public Slice<byte> AppendUint64(Slice<byte> b, ulong v)
        {
            return Slice<byte>.Append(b,
                (byte)(v >> 56), (byte)(v >> 48), (byte)(v >> 40), (byte)(v >> 32),
                (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }
    }

    [GoType("struct", Name = "littleEndian", Package = "encoding/binary")]
    public class GoLittleEndian : Encoding.Binary.Package.IByteOrder, Encoding.Binary.Package.IAppendByteOrder
    {
        [GoMethod]
        public ushort Uint16(Slice<byte> b)
        {
            return (ushort)(b[0] | (b[1] << 8));
        }

        [GoMethod]
        public uint Uint32(Slice<byte> b)
        {
            return b[0] | ((uint)b[1] << 8) | ((uint)b[2] << 16) | ((uint)b[3] << 24);
        }

        [GoMethod]
        public ulong Uint64(Slice<byte> b)
        {
            return b[0] | ((ulong)b[1] << 8) | ((ulong)b[2] << 16) | ((ulong)b[3] << 24) |
                   ((ulong)b[4] << 32) | ((ulong)b[5] << 40) | ((ulong)b[6] << 48) | ((ulong)b[7] << 56);
        }

        [GoMethod]
        public void PutUint16(Slice<byte> b, ushort v)
        {
            b[0] = (byte)v;
            b[1] = (byte)(v >> 8);
        }

        [GoMethod]
        public void PutUint32(Slice<byte> b, uint v)
        {
            b[0] = (byte)v;
            b[1] = (byte)(v >> 8);
            b[2] = (byte)(v >> 16);
            b[3] = (byte)(v >> 24);
        }

        [GoMethod]
        public void PutUint64(Slice<byte> b, ulong v)
        {
            b[0] = (byte)v;
            b[1] = (byte)(v >> 8);
            b[2] = (byte)(v >> 16);
            b[3] = (byte)(v >> 24);
            b[4] = (byte)(v >> 32);
            b[5] = (byte)(v >> 40);
            b[6] = (byte)(v >> 48);
            b[7] = (byte)(v >> 56);
        }

        [GoMethod]
        public string String() => "LittleEndian";

        [GoMethod]
        public Slice<byte> AppendUint16(Slice<byte> b, ushort v)
        {
            return Slice<byte>.Append(b, (byte)v, (byte)(v >> 8));
        }

        [GoMethod]
        public Slice<byte> AppendUint32(Slice<byte> b, uint v)
        {
            return Slice<byte>.Append(b, (byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24));
        }

        [GoMethod]
        public Slice<byte> AppendUint64(Slice<byte> b, ulong v)
        {
            return Slice<byte>.Append(b,
                (byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24),
                (byte)(v >> 32), (byte)(v >> 40), (byte)(v >> 48), (byte)(v >> 56));
        }
    }

    // NativeEndian matches the host system's byte order
    [GoType("struct", Name = "nativeEndian", Package = "encoding/binary")]
    public class GoNativeEndian : Encoding.Binary.Package.IByteOrder
    {
        [GoMethod]
        public ushort Uint16(Slice<byte> b)
        {
            if (BitConverter.IsLittleEndian)
            {
                return (ushort)(b[0] | (b[1] << 8));
            }
            return (ushort)((b[0] << 8) | b[1]);
        }

        [GoMethod]
        public uint Uint32(Slice<byte> b)
        {
            if (BitConverter.IsLittleEndian)
            {
                return b[0] | ((uint)b[1] << 8) | ((uint)b[2] << 16) | ((uint)b[3] << 24);
            }
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        }

        [GoMethod]
        public ulong Uint64(Slice<byte> b)
        {
            if (BitConverter.IsLittleEndian)
            {
                return b[0] | ((ulong)b[1] << 8) | ((ulong)b[2] << 16) | ((ulong)b[3] << 24) |
                       ((ulong)b[4] << 32) | ((ulong)b[5] << 40) | ((ulong)b[6] << 48) | ((ulong)b[7] << 56);
            }
            return ((ulong)b[0] << 56) | ((ulong)b[1] << 48) | ((ulong)b[2] << 40) | ((ulong)b[3] << 32) |
                   ((ulong)b[4] << 24) | ((ulong)b[5] << 16) | ((ulong)b[6] << 8) | b[7];
        }

        [GoMethod]
        public void PutUint16(Slice<byte> b, ushort v)
        {
            if (BitConverter.IsLittleEndian)
            {
                b[0] = (byte)v;
                b[1] = (byte)(v >> 8);
            }
            else
            {
                b[0] = (byte)(v >> 8);
                b[1] = (byte)v;
            }
        }

        [GoMethod]
        public void PutUint32(Slice<byte> b, uint v)
        {
            if (BitConverter.IsLittleEndian)
            {
                b[0] = (byte)v;
                b[1] = (byte)(v >> 8);
                b[2] = (byte)(v >> 16);
                b[3] = (byte)(v >> 24);
            }
            else
            {
                b[0] = (byte)(v >> 24);
                b[1] = (byte)(v >> 16);
                b[2] = (byte)(v >> 8);
                b[3] = (byte)v;
            }
        }

        [GoMethod]
        public void PutUint64(Slice<byte> b, ulong v)
        {
            if (BitConverter.IsLittleEndian)
            {
                b[0] = (byte)v;
                b[1] = (byte)(v >> 8);
                b[2] = (byte)(v >> 16);
                b[3] = (byte)(v >> 24);
                b[4] = (byte)(v >> 32);
                b[5] = (byte)(v >> 40);
                b[6] = (byte)(v >> 48);
                b[7] = (byte)(v >> 56);
            }
            else
            {
                b[0] = (byte)(v >> 56);
                b[1] = (byte)(v >> 48);
                b[2] = (byte)(v >> 40);
                b[3] = (byte)(v >> 32);
                b[4] = (byte)(v >> 24);
                b[5] = (byte)(v >> 16);
                b[6] = (byte)(v >> 8);
                b[7] = (byte)v;
            }
        }

        [GoMethod]
        public string String() => "NativeEndian";
    }
}
