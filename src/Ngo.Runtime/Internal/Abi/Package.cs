using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Abi
{
    [GoPackage("internal/abi")]
    public static class Package
    {
        // Function/method ABI constants (removed — now functions below)

        // Type kind constants (mirror reflect.Kind values)
        [GoConst] public const long Invalid = 0;
        [GoConst] public const long Bool = 1;
        [GoConst] public const long Int = 2;
        [GoConst] public const long Int8 = 3;
        [GoConst] public const long Int16 = 4;
        [GoConst] public const long Int32 = 5;
        [GoConst] public const long Int64 = 6;
        [GoConst] public const long Uint = 7;
        [GoConst] public const long Uint8 = 8;
        [GoConst] public const long Uint16 = 9;
        [GoConst] public const long Uint32 = 10;
        [GoConst] public const long Uint64 = 11;
        [GoConst] public const long Uintptr = 12;
        [GoConst] public const long Float32 = 13;
        [GoConst] public const long Float64 = 14;
        [GoConst] public const long Complex64 = 15;
        [GoConst] public const long Complex128 = 16;
        [GoConst] public const long Array = 17;
        [GoConst] public const long Chan = 18;
        [GoConst] public const long Func = 19;
        [GoConst] public const long Interface = 20;
        [GoConst] public const long Map = 21;
        [GoConst] public const long Pointer = 22;
        [GoConst] public const long Slice = 23;
        [GoConst] public const long String = 24;
        [GoConst] public const long Struct = 25;
        [GoConst] public const long UnsafePointer = 26;
        [GoConst] public const long KindMask = 31;
        [GoConst] public const long KindDirectIface = 32;
        [GoConst] public const long TFlagUncommon = 1;
        [GoConst] public const long TFlagExtraStar = 2;
        [GoConst] public const long TFlagNamed = 4;
        [GoConst] public const long TFlagRegularMemory = 8;

        // Stack/register size constants (amd64)
        [GoConst] public const long PtrSize = 8;
        [GoConst] public const long StackAlign = 16;
        [GoConst] public const long IntArgRegs = 9;
        [GoConst] public const long FloatArgRegs = 15;
        [GoConst] public const long EffectiveFloatRegSize = 8;

        // Map constants
        [GoConst] public const long MapBucketCountBits = 3;
        [GoConst] public const long MapBucketCount = 8;
        [GoConst] public const long MapMaxKeyBytes = 128;
        [GoConst] public const long MapMaxElemBytes = 128;

        [GoFunc]
        public static long FuncPCABI0Offset() => 0;

        // FuncPCABIInternal returns the entry PC of the function f.
        // On .NET, function PCs don't have meaning — return 0.
        [GoFunc]
        [return: GoReturn("uintptr")]
        public static long FuncPCABIInternal([GoParam("interface{}")] object? f) => 0;

        [GoFunc]
        [return: GoReturn("uintptr")]
        public static long FuncPCABI0([GoParam("interface{}")] object? f) => 0;

        [GoFunc]
        [return: GoReturn("*Name")]
        public static GoName? NewName(string n, string tag, bool exported, bool embedded) => new GoName();
    }

    // Named type aliases for offsets
    [GoType("named", Name = "NameOff", Package = "internal/abi", Underlying = "int32")]
    public struct GoNameOff { }

    [GoType("named", Name = "TypeOff", Package = "internal/abi", Underlying = "int32")]
    public struct GoTypeOff { }

    [GoType("named", Name = "TextOff", Package = "internal/abi", Underlying = "int32")]
    public struct GoTextOff { }

    [GoType("named", Name = "Kind", Package = "internal/abi", Underlying = "uint")]
    public struct GoKindType { }

    [GoType("named", Name = "TFlag", Package = "internal/abi", Underlying = "uint8")]
    public struct GoTFlagType { }

    [GoType("named", Name = "ChanDir", Package = "internal/abi", Underlying = "int")]
    public struct GoChanDirType { }

    // abi.Type — the core runtime type descriptor
    [GoType("struct", Name = "Type", Package = "internal/abi")]
    public class GoType
    {
        [GoField(Name = "Size_", Type = "uintptr")] public long Size_;
        [GoField(Name = "PtrBytes", Type = "uintptr")] public long PtrBytes;
        [GoField(Name = "Hash")] public long Hash;
        [GoField(Name = "TFlag")] public byte TFlag;
        [GoField(Name = "Align_")] public byte Align_;
        [GoField(Name = "FieldAlign_")] public byte FieldAlign_;
        [GoField(Name = "Kind_")] public byte Kind_;
        [GoField(Name = "Equal")] public object? Equal; // func(unsafe.Pointer, unsafe.Pointer) bool
        [GoField(Name = "GCData")] public long GCData; // *byte
        [GoField(Name = "Str")] public int Str; // NameOff
        [GoField(Name = "PtrToThis")] public int PtrToThis; // TypeOff

        [GoMethod] public long Size() => Size_;
        [GoMethod] [return: GoReturn("Kind")] public byte Kind() => (byte)(Kind_ & 31);
        [GoMethod] public bool Pointers() => PtrBytes != 0;
        [GoMethod] public bool IfaceIndir() => (Kind_ & 32) != 0;
        [GoMethod] public bool IsDirectIface() => (Kind_ & 32) != 0;
        [GoMethod] public long Align() => Align_;
        [GoMethod] public long FieldAlign() => FieldAlign_;
        [GoMethod] public bool HasName() => (TFlag & 4) != 0;
    }

    // abi.Name — type name descriptor
    [GoType("struct", Name = "Name", Package = "internal/abi")]
    public class GoName
    {
        [GoMethod] public string Name() => "";
        [GoMethod] public string Tag() => "";
        [GoMethod] public bool IsExported() => false;
        [GoMethod] public bool HasTag() => false;
        [GoMethod] public bool IsEmbedded() => false;
        [GoMethod] public bool IsBlank() => false;
    }

    [GoType("struct", Name = "Method", Package = "internal/abi")]
    public class GoMethod
    {
        [GoField(Name = "Name")] public int NameField; // NameOff
        [GoField(Name = "Mtyp")] public int Mtyp; // TypeOff
        [GoField(Name = "Ifn")] public int Ifn; // TextOff
        [GoField(Name = "Tfn")] public int Tfn; // TextOff
    }

    [GoType("struct", Name = "UncommonType", Package = "internal/abi")]
    public class GoUncommonType
    {
        [GoField(Name = "PkgPath")] public int PkgPath; // NameOff
        [GoField(Name = "Mcount")] public int Mcount;
        [GoField(Name = "Xcount")] public int Xcount;
        [GoField(Name = "Moff")] public int Moff;
    }

    [GoType("struct", Name = "Imethod", Package = "internal/abi")]
    public class GoImethod
    {
        [GoField(Name = "Name")] public int NameField; // NameOff
        [GoField(Name = "Typ")] public int Typ; // TypeOff
    }

    [GoType("struct", Name = "ArrayType", Package = "internal/abi")]
    public class GoArrayType
    {
        [GoField(Name = "Elem")] public GoType? Elem;
        [GoField(Name = "Slice")] public GoType? Slice;
        [GoField(Name = "Len", Type = "uintptr")] public long Len;
    }

    [GoType("struct", Name = "ChanType", Package = "internal/abi")]
    public class GoChanType
    {
        [GoField(Name = "Elem")] public GoType? Elem;
        [GoField(Name = "Dir")] public long Dir;
    }

    [GoType("struct", Name = "FuncType", Package = "internal/abi")]
    public class GoFuncType
    {
        [GoField(Name = "InCount")] public int InCount;
        [GoField(Name = "OutCount")] public int OutCount;
    }

    [GoType("struct", Name = "InterfaceType", Package = "internal/abi")]
    public class GoInterfaceType
    {
        [GoField(Name = "PkgPath")] public GoName? PkgPath;
        [GoField(Name = "Methods", Type = "[]Imethod")] public Slice<GoImethod> Methods;
    }

    [GoType("struct", Name = "MapType", Package = "internal/abi")]
    public class GoMapType
    {
        [GoField(Name = "Key")] public GoType? Key;
        [GoField(Name = "Elem")] public GoType? Elem;
        [GoField(Name = "Bucket")] public GoType? Bucket;
        [GoField(Name = "Hasher")] public object? Hasher;
        [GoField(Name = "KeySize")] public byte KeySize;
        [GoField(Name = "ValueSize")] public byte ValueSize;
        [GoField(Name = "BucketSize")] public int BucketSize;
        [GoField(Name = "Flags")] public int Flags;
    }

    [GoType("struct", Name = "PtrType", Package = "internal/abi")]
    public class GoPtrType
    {
        [GoField(Name = "Elem")] public GoType? Elem;
    }

    [GoType("struct", Name = "SliceType", Package = "internal/abi")]
    public class GoSliceType
    {
        [GoField(Name = "Elem")] public GoType? Elem;
    }

    [GoType("struct", Name = "StructField", Package = "internal/abi")]
    public class GoStructField
    {
        [GoField(Name = "Name")] public GoName? Name;
        [GoField(Name = "Typ")] public GoType? Typ;
        [GoField(Name = "Offset", Type = "uintptr")] public long Offset;
    }

    [GoType("struct", Name = "StructType", Package = "internal/abi")]
    public class GoStructType
    {
        [GoField(Name = "PkgPath")] public GoName? PkgPath;
        [GoField(Name = "Fields", Type = "[]StructField")] public Slice<GoStructField> Fields;
    }
}
