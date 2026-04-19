using System;
using System.Diagnostics;
using System.IO;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Os.Exec
{
    [GoType("struct", Name = "Cmd", Package = "os/exec")]
    public class GoCmd
    {
        [GoField]
        public string Path = "";

        [GoField]
        public Slice<string> Args;

        [GoField]
        public Slice<string> Env;

        [GoField]
        public string Dir = "";

        [GoField]
        public object? Stdin;

        [GoField]
        public object? Stdout;

        [GoField]
        public object? Stderr;

        [GoField(Name = "SysProcAttr", Type = "*syscall.SysProcAttr")]
        public object? SysProcAttr;

        [GoField(Name = "Process", Type = "*os.Process")]
        public object? Process;

        [GoField(Name = "ExtraFiles", Type = "[]*os.File")]
        public object? ExtraFiles;

        [GoField(Name = "Err", Type = "error")]
        public object? Err;

        private System.Diagnostics.Process? _process;

        private ProcessStartInfo BuildStartInfo(bool redirectStdout, bool redirectStderr, bool redirectStdin)
        {
            var psi = new ProcessStartInfo(Path)
            {
                UseShellExecute = false,
                RedirectStandardOutput = redirectStdout,
                RedirectStandardError = redirectStderr,
                RedirectStandardInput = redirectStdin,
                WorkingDirectory = Dir ?? "",
            };
            if (!Args.IsNil)
            {
                for (int i = 1; i < Args.Len; i++)
                {
                    psi.ArgumentList.Add(Args[i]);
                }
            }
            if (!Env.IsNil)
            {
                psi.Environment.Clear();
                for (int i = 0; i < Env.Len; i++)
                {
                    int eq = Env[i].IndexOf('=');
                    if (eq >= 0)
                    {
                        psi.Environment[Env[i].Substring(0, eq)] = Env[i].Substring(eq + 1);
                    }
                }
            }
            return psi;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string Run()
        {
            try
            {
                var psi = BuildStartInfo(true, true, false);
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null)
                {
                    return "exec: failed to start process";
                }

                // If Stdout/Stderr are set, pipe output to them
                if (Stdout is IGoWriter stdoutWriter)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    var bytes = System.Text.Encoding.UTF8.GetBytes(output);
                    stdoutWriter.Write(new Slice<byte>(bytes));
                }

                if (Stderr is IGoWriter stderrWriter)
                {
                    var errOutput = proc.StandardError.ReadToEnd();
                    var bytes = System.Text.Encoding.UTF8.GetBytes(errOutput);
                    stderrWriter.Write(new Slice<byte>(bytes));
                }

                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    return $"exit status {proc.ExitCode}";
                }
                return null!;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string Start()
        {
            try
            {
                var psi = BuildStartInfo(
                    Stdout != null || Stdout is IGoWriter,
                    Stderr != null || Stderr is IGoWriter,
                    Stdin != null || Stdin is IGoReader);
                _process = System.Diagnostics.Process.Start(psi);
                if (_process == null)
                {
                    return "exec: failed to start process";
                }
                return null!;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string Wait()
        {
            if (_process == null)
            {
                return "exec: not started";
            }
            try
            {
                // Pipe stdout/stderr if writers are set
                if (Stdout is IGoWriter stdoutWriter && _process.StartInfo.RedirectStandardOutput)
                {
                    var output = _process.StandardOutput.ReadToEnd();
                    var bytes = System.Text.Encoding.UTF8.GetBytes(output);
                    stdoutWriter.Write(new Slice<byte>(bytes));
                }

                if (Stderr is IGoWriter stderrWriter && _process.StartInfo.RedirectStandardError)
                {
                    var errOutput = _process.StandardError.ReadToEnd();
                    var bytes = System.Text.Encoding.UTF8.GetBytes(errOutput);
                    stderrWriter.Write(new Slice<byte>(bytes));
                }

                _process.WaitForExit();
                if (_process.ExitCode != 0)
                {
                    return $"exit status {_process.ExitCode}";
                }
                return null!;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, string) Output()
        {
            try
            {
                var psi = BuildStartInfo(true, true, false);
                using var proc = System.Diagnostics.Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                proc?.WaitForExit();
                var bytes = System.Text.Encoding.UTF8.GetBytes(output);
                if (proc != null && proc.ExitCode != 0)
                {
                    return (new Slice<byte>(bytes), $"exit status {proc.ExitCode}");
                }
                return (new Slice<byte>(bytes), null!);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, string) CombinedOutput()
        {
            try
            {
                var psi = BuildStartInfo(true, true, false);
                psi.RedirectStandardError = true;
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null)
                {
                    return (new Slice<byte>(Array.Empty<byte>()), "exec: failed to start process");
                }
                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                var combined = stdout + stderr;
                var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
                if (proc.ExitCode != 0)
                {
                    return (new Slice<byte>(bytes), $"exit status {proc.ExitCode}");
                }
                return (new Slice<byte>(bytes), null!);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("io.WriteCloser", "error")]
        public (object?, object?) StdinPipe()
        {
            try
            {
                var psi = BuildStartInfo(false, false, true);
                _process = System.Diagnostics.Process.Start(psi);
                if (_process == null)
                {
                    return (null, "exec: failed to start process");
                }
                return (new StreamWriterAdapter(_process.StandardInput.BaseStream), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("io.ReadCloser", "error")]
        public (object?, object?) StdoutPipe()
        {
            try
            {
                var psi = BuildStartInfo(true, false, false);
                _process = System.Diagnostics.Process.Start(psi);
                if (_process == null)
                {
                    return (null, "exec: failed to start process");
                }
                return (new StreamReaderAdapter(_process.StandardOutput.BaseStream), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("io.ReadCloser", "error")]
        public (object?, object?) StderrPipe()
        {
            try
            {
                var psi = BuildStartInfo(false, true, false);
                _process = System.Diagnostics.Process.Start(psi);
                if (_process == null)
                {
                    return (null, "exec: failed to start process");
                }
                return (new StreamReaderAdapter(_process.StandardError.BaseStream), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        public string String()
        {
            return Path;
        }

        [GoMethod]
        public Slice<string> Environ()
        {
            if (!Env.IsNil && Env.Len > 0)
            {
                return Env;
            }
            var envVars = Environment.GetEnvironmentVariables();
            var list = new System.Collections.Generic.List<string>();
            foreach (System.Collections.DictionaryEntry entry in envVars)
            {
                list.Add($"{entry.Key}={entry.Value}");
            }
            return new Slice<string>(list.ToArray());
        }
    }

    /// <summary>
    /// Adapts a .NET Stream to Go's io.Writer + io.Closer.
    /// </summary>
    internal class StreamWriterAdapter : IGoWriter, IGoCloser
    {
        private readonly Stream _stream;

        public StreamWriterAdapter(Stream stream)
        {
            _stream = stream;
        }

        public (long, string) Write(Slice<byte> p)
        {
            var buf = new byte[p.Len];
            for (int i = 0; i < p.Len; i++)
            {
                buf[i] = p[i];
            }
            _stream.Write(buf, 0, buf.Length);
            return (p.Len, null!);
        }

        public string Close()
        {
            try
            {
                _stream.Close();
                return null!;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
