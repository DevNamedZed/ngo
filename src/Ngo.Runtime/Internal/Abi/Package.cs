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

        [GoFunc]
        [return: GoReturn("*Type")]
        public static GoType TypeOf([GoParam("any")] object? a)
        {
            var type = a?.GetType() ?? typeof(object);
            return new GoType { ClrType = type };
        }

        public static GoType TypeForType(System.Type t)
        {
            if (t == null)
            {
                throw new System.ArgumentNullException(nameof(t));
            }
            return new GoType { ClrType = t };
        }
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

        // CLR type backing this abi.Type — set by reflect package
        internal System.Type? ClrType;
        internal GoType? ElemType;
        internal GoType? KeyType;
        internal GoType[]? FieldTypes;
        internal GoType[]? InTypes;
        internal GoType[]? OutTypes;

        // Go embedding: sub-types embed abi.Type, accessible as .Type
        [GoField(Name = "Type")] public GoType TypeEmbedded => this;

        [GoMethod] public long Size() => Size_;
        [GoMethod] [return: GoReturn("Kind")] public byte Kind() => (byte)(Kind_ & 31);
        [GoMethod] public bool Pointers() => PtrBytes != 0;
        [GoMethod] public bool IfaceIndir() => (Kind_ & 32) != 0;
        [GoMethod] public bool IsDirectIface() => (Kind_ & 32) != 0;
        [GoMethod] public long Align() => Align_;
        [GoMethod] public long FieldAlign() => FieldAlign_;
        [GoMethod] public bool HasName() => (TFlag & 4) != 0;

        [GoMethod]
        [return: GoReturn("*Type")]
        public GoType? Elem() => ElemType;

        [GoMethod]
        [return: GoReturn("*UncommonType")]
        public GoUncommonType? Uncommon()
        {
            if (ClrType != null && ClrType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Length > 0)
            {
                return new GoUncommonType();
            }
            return null;
        }

        [GoMethod] [return: GoReturn("ChanDir")] public long ChanDir() => 0;
        [GoMethod] [return: GoReturn("int")] public long Len() => 0;
        [GoMethod] [return: GoReturn("uint32")] public long NumMethod()
        {
            if (ClrType != null)
            {
                return ClrType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly).Length;
            }
            return 0;
        }

        [GoMethod]
        public bool ExportedMethods() => NumMethod() > 0;

        [GoMethod]
        [return: GoReturn("*Type")]
        public GoType? Key() => KeyType;

        [GoMethod]
        [return: GoReturn("*Type")]
        public GoType? Field([GoParam("int")] long i)
        {
            if (FieldTypes != null && i >= 0 && i < FieldTypes.Length)
            {
                return FieldTypes[i];
            }
            return null;
        }

        [GoMethod] public bool Comparable() => true;
        [GoMethod] public string String() => ClrType?.Name ?? "";
        [GoMethod] [return: GoReturn("[]byte")] public Slice<byte> GcSlice([GoParam("uintptr")] long begin, [GoParam("uintptr")] long end) => default;
        [GoMethod] [return: GoReturn("*Type")] public GoType Common() => this;

        // Go's (*Type).MapType() reinterprets a map Type's header as *MapType
        // ((*MapType)(unsafe.Pointer(t))). GoMapType derives from GoType, so the
        // reinterpret is a downcast; a non-map Type yields nil, as in Go.
        [GoMethod] [return: GoReturn("*MapType")] public GoMapType? MapType() => this as GoMapType;
    }

    // abi.Name — type name descriptor
    [GoType("struct", Name = "Name", Package = "internal/abi")]
    public class GoName
    {
        [GoField(Name = "Bytes")] public long Bytes; // *byte pointer

        internal string NameValue = "";
        internal string TagValue = "";
        internal bool IsExportedValue;
        internal bool HasTagValue;
        internal bool IsEmbeddedValue;

        [GoMethod] public string Name() => NameValue;
        [GoMethod] public string Tag() => TagValue;
        [GoMethod] public bool IsExported() => IsExportedValue || (NameValue.Length > 0 && char.IsUpper(NameValue[0]));
        [GoMethod] public bool HasTag() => HasTagValue || TagValue.Length > 0;
        [GoMethod] public bool IsEmbedded() => IsEmbeddedValue;
        [GoMethod] public bool IsBlank() => NameValue == "_";
        [GoMethod] [return: GoReturn("int")] public long DataChecked([GoParam("int")] long off, string msg) => 0;
        [GoMethod]
        [return: GoReturn("int", "int")]
        public (long, long) ReadVarint([GoParam("int")] long off)
        {
            // Go's abi.Name.ReadVarint decodes a varint from binary data
            // Since we store names as managed strings, return the name length
            long value = NameValue.Length;
            return (value, 1);
        }
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

        [GoMethod] [return: GoReturn("[]Method")] public Slice<GoMethod> Methods() => default;
        [GoMethod] [return: GoReturn("[]Method")] public Slice<GoMethod> ExportedMethods() => default;
    }

    [GoType("struct", Name = "Imethod", Package = "internal/abi")]
    public class GoImethod
    {
        [GoField(Name = "Name")] public int NameField; // NameOff
        [GoField(Name = "Typ")] public int Typ; // TypeOff
    }

    [GoType("struct", Name = "ArrayType", Package = "internal/abi")]
    public class GoArrayType : GoType
    {
        [GoField(Name = "Elem")] public new GoType? Elem;
        [GoField(Name = "Slice")] public GoType? Slice;
        [GoField(Name = "Len", Type = "uintptr")] public new long Len;
    }

    [GoType("struct", Name = "ChanType", Package = "internal/abi")]
    public class GoChanType : GoType
    {
        [GoField(Name = "Elem")] public GoType? ChanElem;
        [GoField(Name = "Dir")] public new long ChanDir;
    }

    [GoType("struct", Name = "FuncType", Package = "internal/abi")]
    public class GoFuncType : GoType
    {
        [GoField(Name = "InCount")] public int InCount;
        [GoField(Name = "OutCount")] public int OutCount;

        [GoMethod] [return: GoReturn("int")] public long NumIn() => InCount;
        [GoMethod] [return: GoReturn("int")] public long NumOut() => OutCount & ((1 << 5) - 1);
        [GoMethod] public bool IsVariadic() => (OutCount & (1 << 7)) != 0;
        [GoMethod] [return: GoReturn("[]*Type")] public Slice<GoType> InSlice()
        {
            if (InTypes != null)
            {
                return new Slice<GoType>(InTypes);
            }
            return default;
        }

        [GoMethod] [return: GoReturn("[]*Type")] public Slice<GoType> OutSlice()
        {
            if (OutTypes != null)
            {
                return new Slice<GoType>(OutTypes);
            }
            return default;
        }

        [GoMethod]
        [return: GoReturn("*Type")]
        public GoType? In([GoParam("int")] long i)
        {
            if (InTypes != null && i >= 0 && i < InTypes.Length)
            {
                return InTypes[i];
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("*Type")]
        public GoType? Out([GoParam("int")] long i)
        {
            if (OutTypes != null && i >= 0 && i < OutTypes.Length)
            {
                return OutTypes[i];
            }
            return null;
        }
    }

    [GoType("struct", Name = "InterfaceType", Package = "internal/abi")]
    public class GoInterfaceType : GoType
    {
        [GoField(Name = "PkgPath")] public GoName? PkgPath;
        [GoField(Name = "Methods", Type = "[]Imethod")] public Slice<GoImethod> Methods;
    }

    [GoType("struct", Name = "MapType", Package = "internal/abi")]
    public class GoMapType : GoType
    {
        [GoField(Name = "Key")] public new GoType? Key;
        [GoField(Name = "Elem")] public new GoType? Elem;
        [GoField(Name = "Bucket")] public GoType? Bucket;
        [GoField(Name = "Hasher")] public object? Hasher;
        [GoField(Name = "KeySize")] public byte KeySize;
        [GoField(Name = "ValueSize")] public byte ValueSize;
        [GoField(Name = "BucketSize")] public int BucketSize;
        [GoField(Name = "Flags")] public int Flags;
    }

    [GoType("struct", Name = "PtrType", Package = "internal/abi")]
    public class GoPtrType : GoType
    {
        [GoField(Name = "Elem")] public new GoType? Elem;
    }

    [GoType("struct", Name = "SliceType", Package = "internal/abi")]
    public class GoSliceType : GoType
    {
        [GoField(Name = "Elem")] public new GoType? Elem;
    }

    [GoType("struct", Name = "StructField", Package = "internal/abi")]
    public class GoStructField
    {
        [GoField(Name = "Name")] public GoName? Name;
        [GoField(Name = "Typ")] public GoType? Typ;
        [GoField(Name = "Offset", Type = "uintptr")] public long Offset;
        [GoField(Name = "Embedded")] public bool Embedded;
    }

    [GoType("struct", Name = "StructType", Package = "internal/abi")]
    public class GoStructType : GoType
    {
        [GoField(Name = "PkgPath")] public GoName? PkgPath;
        [GoField(Name = "Fields", Type = "[]StructField")] public Slice<GoStructField> Fields;
    }
}
