using System;
using System.Diagnostics;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os.Exec
{
    /// <summary>
    /// Runtime support for Go's os/exec package.
    /// </summary>
    [GoPackage("os/exec")]
    public static class Package
    {
        [GoVar(Type = "error")]
        public static readonly object ErrNotFound = new Exception("executable file not found in $PATH");

        [GoVar(Type = "error")]
        public static readonly object ErrDot = new Exception("cannot run executable found relative to current directory");

        [GoFunc(IsVariadic = true)]
        public static GoCmd Command(string name, params string[] arg)
        {
            var cmd = new GoCmd();
            cmd.Path = name;
            var allArgs = new string[1 + (arg?.Length ?? 0)];
            allArgs[0] = name;
            if (arg != null)
            {
                for (int i = 0; i < arg.Length; i++)
                {
                    allArgs[i + 1] = arg[i];
                }
            }
            cmd.Args = new Slice<string>(allArgs);
            cmd.Env = new Slice<string>(Array.Empty<string>());
            cmd.Dir = "";
            return cmd;
        }

        [GoFunc(IsVariadic = true)]
        public static GoCmd CommandContext(object ctx, string name, params string[] arg)
        {
            return Command(name, arg);
        }

        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, string) LookPath(string file)
        {
            // Simple implementation: check if file exists or is in PATH
            try
            {
                var path = file;
                if (System.IO.File.Exists(path))
                    return (path, null!);

                var envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                var separator = System.IO.Path.PathSeparator;
                foreach (var dir in envPath.Split(separator))
                {
                    var fullPath = System.IO.Path.Combine(dir, file);
                    if (System.IO.File.Exists(fullPath))
                        return (fullPath, null!);
                }
                return ("", $"exec: \"{file}\": executable file not found in $PATH");
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }
    }
}
