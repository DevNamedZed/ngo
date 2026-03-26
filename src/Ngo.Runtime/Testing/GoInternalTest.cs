using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Testing
{
    [GoType("struct", Name = "InternalTest", Package = "testing")]
    public class GoInternalTest
    {
        [GoField] public string Name = "";
        [GoField(Name = "F", Type = "func(*testing.T)")] public object? F;
    }
}
