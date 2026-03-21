using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net.Mail
{
    /// <summary>
    /// Runtime support for Go's net/mail package.
    /// </summary>
    [GoPackage("net/mail")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*Address", "error")]
        public static (GoAddress, string) ParseAddress(string address)
        {
            // Simple parser for "Name <email>" format
            try
            {
                var addr = new GoAddress();
                var trimmed = address.Trim();
                var ltIdx = trimmed.IndexOf('<');
                if (ltIdx >= 0)
                {
                    addr.Name = trimmed.Substring(0, ltIdx).Trim().Trim('"');
                    var gtIdx = trimmed.IndexOf('>', ltIdx);
                    addr.Address = trimmed.Substring(ltIdx + 1, (gtIdx < 0 ? trimmed.Length : gtIdx) - ltIdx - 1);
                }
                else
                {
                    addr.Address = trimmed;
                    addr.Name = "";
                }
                return (addr, null!);
            }
            catch (Exception ex)
            {
                return (null!, ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("[]*Address", "error")]
        public static (Slice<GoAddress>, string) ParseAddressList(string list)
        {
            try
            {
                var parts = list.Split(',');
                var addrs = new GoAddress[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    var (addr, err) = ParseAddress(parts[i]);
                    if (err != null)
                        return (new Slice<GoAddress>(Array.Empty<GoAddress>()), err);
                    addrs[i] = addr;
                }
                return (new Slice<GoAddress>(addrs), null!);
            }
            catch (Exception ex)
            {
                return (new Slice<GoAddress>(Array.Empty<GoAddress>()), ex.Message);
            }
        }
    }

    [GoType("struct", Name = "Address", Package = "net/mail")]
    public class GoAddress
    {
        [GoField]
        public string Name;

        [GoField]
        public string Address;

        [GoMethod]
        public string String()
        {
            if (string.IsNullOrEmpty(Name))
                return $"<{Address}>";
            return $"\"{Name}\" <{Address}>";
        }
    }
}
