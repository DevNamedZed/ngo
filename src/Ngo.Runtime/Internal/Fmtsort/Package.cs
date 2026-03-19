using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Fmtsort
{
    /// <summary>
    /// internal/fmtsort — sorts map keys for deterministic fmt output.
    /// </summary>
    [GoPackage("internal/fmtsort")]
    public static class Package
    {
        // Sort sorts map entries by key. Returns []KeyValue (a struct with Key, Value reflect.Value fields).
        [GoFunc]
        [return: GoReturn("[]KeyValue")]
        public static Slice<GoKeyValue> Sort([GoParam("reflect.Value")] object? mapValue)
        {
            return default;
        }

        // SortedKeys returns sorted keys from a map reflect.Value (legacy)
        [GoFunc]
        [return: GoReturn("[]reflect.Value")]
        public static Slice<object> SortedKeys([GoParam("reflect.Value")] object? mapValue)
        {
            return default;
        }
    }

    [GoType("struct", Name = "KeyValue", Package = "internal/fmtsort")]
    public class GoKeyValue
    {
        [GoField(Name = "Key", Type = "reflect.Value")] public object? Key;
        [GoField(Name = "Value", Type = "reflect.Value")] public object? Value;
    }
}
