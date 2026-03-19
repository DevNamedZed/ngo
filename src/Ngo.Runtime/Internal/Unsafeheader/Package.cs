using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Unsafeheader
{
    /// <summary>
    /// internal/unsafeheader — unsafe pointer header types for slices and strings.
    /// Used by reflect and runtime to access internal representation.
    /// </summary>
    [GoPackage("internal/unsafeheader")]
    public static class Package { }

    [GoType("struct", Name = "Slice", Package = "internal/unsafeheader")]
    public class GoSliceHeader
    {
        [GoField(Name = "Data")] public long Data; // unsafe.Pointer
        [GoField(Name = "Len")] public long Len;
        [GoField(Name = "Cap")] public long Cap;
    }

    [GoType("struct", Name = "String", Package = "internal/unsafeheader")]
    public class GoStringHeader
    {
        [GoField(Name = "Data")] public long Data; // unsafe.Pointer
        [GoField(Name = "Len")] public long Len;
    }
}
