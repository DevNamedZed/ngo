using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Image.Color
{
    [GoPackage("image/color")]
    public static class Package
    {
        // color.Color interface { RGBA() (r, g, b, a uint32) }
        [GoType("interface", Name = "Color", Package = "image/color")]
        public interface IColor
        {
            [GoMethod]
            [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
            (uint, uint, uint, uint) RGBA();
        }

        // color.Model interface { Convert(c Color) Color }
        [GoType("interface", Name = "Model", Package = "image/color")]
        public interface IModel
        {
            [GoMethod]
            [return: GoReturn("color.Color")]
            object? Convert(object? c);
        }

        // Standard color models
        [GoVar(Type = "color.Model")]
        public static readonly object? RGBAModel = null;

        [GoVar(Type = "color.Model")]
        public static readonly object? RGBA64Model = null;

        [GoVar(Type = "color.Model")]
        public static readonly object? NRGBAModel = null;

        [GoVar(Type = "color.Model")]
        public static readonly object? NRGBA64Model = null;

        [GoVar(Type = "color.Model")]
        public static readonly object? AlphaModel = null;

        [GoVar(Type = "color.Model")]
        public static readonly object? Alpha16Model = null;

        [GoVar(Type = "color.Model")]
        public static readonly object? GrayModel = null;

        [GoVar(Type = "color.Model")]
        public static readonly object? Gray16Model = null;

        [GoVar(Type = "color.Model")]
        public static readonly object? CMYKModel = null;

        [GoVar(Type = "color.Model")]
        public static readonly object? YCbCrModel = null;

        [GoVar(Type = "color.Model")]
        public static readonly object? NYCbCrAModel = null;

        // Standard colors
        [GoVar(Type = "color.Color")]
        public static readonly object? Black = null;

        [GoVar(Type = "color.Color")]
        public static readonly object? White = null;

        [GoVar(Type = "color.Color")]
        public static readonly object? Transparent = null;

        [GoVar(Type = "color.Color")]
        public static readonly object? Opaque = null;

        // Conversion functions
        [GoFunc]
        [return: GoReturn("byte", "byte", "byte", "byte")]
        public static (byte, byte, byte, byte) RGBToCMYK(byte r, byte g, byte b)
        {
            uint w = System.Math.Max(r, System.Math.Max(g, b));
            if (w == 0) return (0, 0, 0, 255);
            byte c = (byte)((w - r) * 255 / w);
            byte m = (byte)((w - g) * 255 / w);
            byte y = (byte)((w - b) * 255 / w);
            byte k = (byte)(255 - w);
            return (c, m, y, k);
        }

        [GoFunc]
        [return: GoReturn("byte", "byte", "byte")]
        public static (byte, byte, byte) RGBToYCbCr(byte r, byte g, byte b)
        {
            int r1 = r;
            int g1 = g;
            int b1 = b;
            int yy = (19595 * r1 + 38470 * g1 + 7471 * b1 + (1 << 15)) >> 16;
            int cb = (-11056 * r1 - 21712 * g1 + 32768 * b1 + (128 << 16) + (1 << 15)) >> 16;
            int cr = (32768 * r1 - 27440 * g1 - 5328 * b1 + (128 << 16) + (1 << 15)) >> 16;
            return ((byte)System.Math.Clamp(yy, 0, 255), (byte)System.Math.Clamp(cb, 0, 255), (byte)System.Math.Clamp(cr, 0, 255));
        }

        [GoFunc]
        [return: GoReturn("byte", "byte", "byte")]
        public static (byte, byte, byte) CMYKToRGB(byte c, byte m, byte y, byte k)
        {
            uint w = (uint)(255 - k);
            byte r = (byte)((255 - c) * w / 255);
            byte g = (byte)((255 - m) * w / 255);
            byte b = (byte)((255 - y) * w / 255);
            return (r, g, b);
        }
    }

    [GoType("struct", Name = "RGBA", Package = "image/color")]
    public struct GoRGBA
    {
        [GoField(Name = "R")] public byte R;
        [GoField(Name = "G")] public byte G;
        [GoField(Name = "B")] public byte B;
        [GoField(Name = "A")] public byte A;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA() => (R, G, B, A);
    }

    [GoType("struct", Name = "NRGBA", Package = "image/color")]
    public struct GoNRGBA
    {
        [GoField(Name = "R")] public byte R;
        [GoField(Name = "G")] public byte G;
        [GoField(Name = "B")] public byte B;
        [GoField(Name = "A")] public byte A;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA() => (R, G, B, A);
    }

    [GoType("struct", Name = "RGBA64", Package = "image/color")]
    public struct GoRGBA64
    {
        [GoField(Name = "R")] public ushort R;
        [GoField(Name = "G")] public ushort G;
        [GoField(Name = "B")] public ushort B;
        [GoField(Name = "A")] public ushort A;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA() => (R, G, B, A);
    }

    [GoType("struct", Name = "NRGBA64", Package = "image/color")]
    public struct GoNRGBA64
    {
        [GoField(Name = "R")] public ushort R;
        [GoField(Name = "G")] public ushort G;
        [GoField(Name = "B")] public ushort B;
        [GoField(Name = "A")] public ushort A;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA() => (R, G, B, A);
    }

    [GoType("struct", Name = "Gray", Package = "image/color")]
    public struct GoGray
    {
        [GoField(Name = "Y")] public byte Y;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA() => (Y, Y, Y, 255);
    }

    [GoType("struct", Name = "Gray16", Package = "image/color")]
    public struct GoGray16
    {
        [GoField(Name = "Y")] public ushort Y;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA() => (Y, Y, Y, 65535);
    }

    [GoType("struct", Name = "Alpha", Package = "image/color")]
    public struct GoAlpha
    {
        [GoField(Name = "A")] public byte A;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA() => (0, 0, 0, A);
    }

    [GoType("struct", Name = "Alpha16", Package = "image/color")]
    public struct GoAlpha16
    {
        [GoField(Name = "A")] public ushort A;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA() => (0, 0, 0, A);
    }

    [GoType("struct", Name = "CMYK", Package = "image/color")]
    public struct GoCMYK
    {
        [GoField(Name = "C")] public byte C;
        [GoField(Name = "M")] public byte M;
        [GoField(Name = "Y")] public byte Y;
        [GoField(Name = "K")] public byte K;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA()
        {
            uint w = (uint)(255 - K);
            uint r = (uint)((255 - C) * w / 255);
            uint g = (uint)((255 - M) * w / 255);
            uint b = (uint)((255 - Y) * w / 255);
            return (r | (r << 8), g | (g << 8), b | (b << 8), 0xFFFF);
        }
    }

    [GoType("struct", Name = "YCbCr", Package = "image/color")]
    public struct GoYCbCr
    {
        [GoField(Name = "Y")] public byte Y;
        [GoField(Name = "Cb")] public byte Cb;
        [GoField(Name = "Cr")] public byte Cr;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA()
        {
            int yy1 = (int)Y * 0x10101;
            int cb1 = (int)Cb - 128;
            int cr1 = (int)Cr - 128;
            uint r = (uint)System.Math.Clamp((yy1 + 91881 * cr1) >> 8, 0, 0xFFFF);
            uint g = (uint)System.Math.Clamp((yy1 - 22554 * cb1 - 46802 * cr1) >> 8, 0, 0xFFFF);
            uint b = (uint)System.Math.Clamp((yy1 + 116130 * cb1) >> 8, 0, 0xFFFF);
            return (r, g, b, 0xFFFF);
        }
    }

    [GoType("struct", Name = "NYCbCrA", Package = "image/color")]
    public struct GoNYCbCrA
    {
        [GoField(Embedded = true)] public GoYCbCr YCbCr;
        [GoField(Name = "A")] public byte A;

        [GoMethod]
        [return: GoReturn("uint32", "uint32", "uint32", "uint32")]
        public (uint, uint, uint, uint) RGBA()
        {
            int yy1 = (int)YCbCr.Y * 0x10101;
            int cb1 = (int)YCbCr.Cb - 128;
            int cr1 = (int)YCbCr.Cr - 128;
            uint r = (uint)System.Math.Clamp((yy1 + 91881 * cr1) >> 8, 0, 0xFFFF);
            uint g = (uint)System.Math.Clamp((yy1 - 22554 * cb1 - 46802 * cr1) >> 8, 0, 0xFFFF);
            uint b = (uint)System.Math.Clamp((yy1 + 116130 * cb1) >> 8, 0, 0xFFFF);
            uint a = (uint)A * 0x101;
            r = r * a / 0xFFFF;
            g = g * a / 0xFFFF;
            b = b * a / 0xFFFF;
            return (r, g, b, a);
        }
    }

    [GoType("named", Name = "Palette", Package = "image/color", Underlying = "[]color.Color")]
    public struct GoPalette
    {
        public Slice<object?> Value;

        public GoPalette(Slice<object?> value) { Value = value; }

        public static implicit operator Slice<object?>(GoPalette p) => p.Value;
        public static implicit operator GoPalette(Slice<object?> s) => new GoPalette(s);

        [GoMethod]
        [return: GoReturn("color.Color")]
        public object? Convert([GoParam("color.Color")] object? c)
        {
            if (Value.Len == 0) return null;
            return Value[(int)Index(c)];
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Index([GoParam("color.Color")] object? c)
        {
            // Simple stub: return index 0
            return 0;
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Len() => Value.Len;
    }
}
