using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "SRV", Package = "net")]
    public class GoSRV
    {
        [GoField(Name = "Target")] public string Target { get; set; } = "";
        [GoField(Name = "Port")] public long Port { get; set; }
        [GoField(Name = "Priority")] public long Priority { get; set; }
        [GoField(Name = "Weight")] public long Weight { get; set; }
    }
}
