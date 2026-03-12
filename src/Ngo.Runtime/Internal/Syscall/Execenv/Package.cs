using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Syscall.Execenv
{
    [GoPackage("internal/syscall/execenv")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("[]string", "error")]
        public static (Slice<string>, object?) Default(object? sys)
        {
            var env = Environment.GetEnvironmentVariables();
            var result = new string[env.Count];
            int i = 0;
            foreach (System.Collections.DictionaryEntry entry in env)
            {
                result[i++] = $"{entry.Key}={entry.Value}";
            }
            return (new Slice<string>(result), null);
        }
    }
}
