using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Flag
{
    // flag.Flag struct
    [GoType("struct", Name = "Flag", Package = "flag")]
    public class GoFlag
    {
        [GoField(Name = "Name")] public string Name { get; set; } = "";
        [GoField(Name = "Usage")] public string Usage { get; set; } = "";
        [GoField(Name = "Value")] public object? Value { get; set; }
        [GoField(Name = "DefValue")] public string DefValue { get; set; } = "";
    }
}
