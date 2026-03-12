using System;
using System.Diagnostics;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os.Exec
{
    [GoType("struct", Name = "Cmd", Package = "os/exec")]
    public class GoCmd
    {
        [GoField]
        public string Path;

        [GoField]
        public Slice<string> Args;

        [GoField]
        public Slice<string> Env;

        [GoField]
        public string Dir;

        [GoField]
        public object Stdin;

        [GoField]
        public object Stdout;

        [GoField]
        public object Stderr;

        [GoField(Name = "Process", Type = "*os.Process")]
        public object? Process;

        [GoMethod]
        public string Run()
        {
            try
            {
                var psi = new ProcessStartInfo(Path)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Dir ?? "",
                };
                if (!Args.IsNil)
                {
                    for (int i = 1; i < Args.Len; i++)
                        psi.ArgumentList.Add(Args[i]);
                }
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit();
                if (proc != null && proc.ExitCode != 0)
                    return $"exit status {proc.ExitCode}";
                return null!;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        public string Start()
        {
            throw new NotImplementedException("exec.Cmd.Start not yet implemented");
        }

        [GoMethod]
        public string Wait()
        {
            throw new NotImplementedException("exec.Cmd.Wait not yet implemented");
        }

        [GoMethod]
        public (Slice<byte>, string) Output()
        {
            try
            {
                var psi = new ProcessStartInfo(Path)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Dir ?? "",
                };
                if (!Args.IsNil)
                {
                    for (int i = 1; i < Args.Len; i++)
                        psi.ArgumentList.Add(Args[i]);
                }
                using var proc = System.Diagnostics.Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                proc?.WaitForExit();
                var bytes = System.Text.Encoding.UTF8.GetBytes(output);
                if (proc != null && proc.ExitCode != 0)
                    return (new Slice<byte>(bytes), $"exit status {proc.ExitCode}");
                return (new Slice<byte>(bytes), null!);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        [GoMethod]
        public (Slice<byte>, string) CombinedOutput()
        {
            return Output(); // Simplified
        }

        [GoMethod]
        [return: GoReturn("io.WriteCloser", "error")]
        public (object, object?) StdinPipe()
        {
            throw new NotImplementedException("exec.Cmd.StdinPipe not yet implemented");
        }

        [GoMethod]
        [return: GoReturn("io.ReadCloser", "error")]
        public (object, object?) StdoutPipe()
        {
            throw new NotImplementedException("exec.Cmd.StdoutPipe not yet implemented");
        }

        [GoMethod]
        [return: GoReturn("io.ReadCloser", "error")]
        public (object, object?) StderrPipe()
        {
            throw new NotImplementedException("exec.Cmd.StderrPipe not yet implemented");
        }

        [GoMethod]
        public string String()
        {
            return Path;
        }

        [GoMethod]
        public Slice<string> Environ()
        {
            // In Go, Cmd.Environ returns the environment that would be used
            // to run the command. If Env is set, it returns that; otherwise
            // it returns the current process environment.
            if (!Env.IsNil && Env.Len > 0)
                return Env;
            var envVars = Environment.GetEnvironmentVariables();
            var list = new System.Collections.Generic.List<string>();
            foreach (System.Collections.DictionaryEntry entry in envVars)
                list.Add($"{entry.Key}={entry.Value}");
            return new Slice<string>(list.ToArray());
        }
    }
}
