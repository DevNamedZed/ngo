using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "Resolver", Package = "net")]
    public class GoResolver
    {
        [GoField(Name = "PreferGo")] public bool PreferGo;
        [GoField(Name = "Dial", Type = "func(ctx context.Context, network, address string) (net.Conn, error)")] public object? Dial;

        [GoMethod]
        [return: GoReturn("[]string", "error")]
        public (Slice<string>, object?) LookupHost([GoParam("context.Context")] object? ctx, string host)
        {
            try
            {
                var addresses = System.Net.Dns.GetHostAddresses(host);
                var result = new string[addresses.Length];
                for (int i = 0; i < addresses.Length; i++)
                    result[i] = addresses[i].ToString();
                return (new Slice<string>(result), null);
            }
            catch (System.Exception ex)
            {
                return (default, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("[]net.IP", "error")]
        public (Slice<object?>, object?) LookupIP([GoParam("context.Context")] object? ctx, string network, string host)
        {
            try
            {
                var addresses = System.Net.Dns.GetHostAddresses(host);
                var result = new object?[addresses.Length];
                for (int i = 0; i < addresses.Length; i++)
                {
                    result[i] = new GoIP();
                }
                return (new Slice<object?>(result), null);
            }
            catch (System.Exception ex)
            {
                return (default, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("[]string", "error")]
        public (Slice<string>, object?) LookupAddr([GoParam("context.Context")] object? ctx, string addr)
        {
            try
            {
                var entry = System.Net.Dns.GetHostEntry(addr);
                return (new Slice<string>(new[] { entry.HostName }), null);
            }
            catch (System.Exception ex)
            {
                return (default, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("string", "[]*net.SRV", "error")]
        public (string, Slice<GoSRV?>, object?) LookupSRV([GoParam("context.Context")] object? ctx, string service, string proto, string name)
        {
            return GoNet.LookupSRV(service, proto, name);
        }

        [GoMethod]
        [return: GoReturn("string", "[]*net.MX", "error")]
        public (string, Slice<GoMX?>, object?) LookupMX([GoParam("context.Context")] object? ctx, string name)
        {
            return ("", new Slice<GoMX?>(System.Array.Empty<GoMX?>()), null);
        }

        [GoMethod]
        [return: GoReturn("[]*net.NS", "error")]
        public (Slice<object?>, object?) LookupNS([GoParam("context.Context")] object? ctx, string name)
        {
            return (new Slice<object?>(System.Array.Empty<object?>()), null);
        }

        [GoMethod]
        [return: GoReturn("[]string", "error")]
        public (Slice<string>, object?) LookupTXT([GoParam("context.Context")] object? ctx, string name)
        {
            return (new Slice<string>(System.Array.Empty<string>()), null);
        }

        [GoMethod]
        [return: GoReturn("[]string", "error")]
        public (Slice<string>, object?) LookupCNAME([GoParam("context.Context")] object? ctx, string name)
        {
            return (new Slice<string>(System.Array.Empty<string>()), null);
        }
    }
}
