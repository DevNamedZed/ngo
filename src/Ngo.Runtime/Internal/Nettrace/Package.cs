using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Nettrace
{
    /// <summary>
    /// internal/nettrace — network tracing hooks for net package.
    /// </summary>
    [GoPackage("internal/nettrace")]
    public static class Package
    {
        [GoType("struct", Name = "TraceKey", Package = "internal/nettrace")]
        public class GoTraceKey { }

        [GoType("struct", Name = "LookupIPAltResolverKey", Package = "internal/nettrace")]
        public class GoLookupIPAltResolverKey { }

        [GoType("struct", Name = "Trace", Package = "internal/nettrace")]
        public class GoTrace
        {
            [GoField(Name = "DNSStart")]
            public object? DNSStart { get; set; }
            [GoField(Name = "DNSDone")]
            public object? DNSDone { get; set; }
            [GoField(Name = "ConnectStart")]
            public object? ConnectStart { get; set; }
            [GoField(Name = "ConnectDone")]
            public object? ConnectDone { get; set; }
        }
    }
}
