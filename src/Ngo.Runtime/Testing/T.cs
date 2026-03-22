using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Testing
{
    [GoType("struct", Name = "T", Package = "testing")]
    public class T
    {
        private readonly string _name;
        private bool _failed;
        private bool _skipped;
        private readonly List<Action> _cleanups = new();
        private readonly List<string> _logs = new();

        public T(string name)
        {
            _name = name;
        }

        public string Name()
        {
            return _name;
        }

        public bool Failed()
        {
            return _failed;
        }

        public bool Skipped()
        {
            return _skipped;
        }

        [GoMethod(IsVariadic = true)]
        public void Log(params object?[] args)
        {
            _logs.Add(string.Join(" ", args.Select(a => a?.ToString() ?? "<nil>")));
        }

        [GoMethod(IsVariadic = true)]
        public void Logf(string format, params object?[] args)
        {
            _logs.Add(Fmt.Package.Sprintf(format, args));
        }

        [GoMethod(IsVariadic = true)]
        public void Error(params object?[] args)
        {
            _failed = true;
            if (args.Length == 1)
            {
                _logs.Add(args[0]?.ToString() ?? "");
            }
            else
            {
                _logs.Add(string.Join(" ", args.Select(a => a?.ToString() ?? "<nil>")));
            }
        }

        [GoMethod(IsVariadic = true)]
        public void Errorf(string format, params object?[] args)
        {
            _failed = true;
            _logs.Add(Fmt.Package.Sprintf(format, args));
        }

        [GoMethod(IsVariadic = true)]
        public void Fatal(params object?[] args)
        {
            _failed = true;
            var msg = string.Join(" ", args.Select(a => a?.ToString() ?? "<nil>"));
            _logs.Add(msg);
            throw new TestFailException(msg);
        }

        [GoMethod(IsVariadic = true)]
        public void Fatalf(string format, params object?[] args)
        {
            _failed = true;
            _logs.Add(Fmt.Package.Sprintf(format, args));
            throw new TestFailException(_logs[_logs.Count - 1]);
        }

        public void Fail()
        {
            _failed = true;
        }

        public void FailNow()
        {
            _failed = true;
            throw new TestFailException("FailNow called");
        }

        [GoMethod]
        public void Skip(object? msg)
        {
            _skipped = true;
            _logs.Add(msg?.ToString() ?? "");
            throw new TestSkipException(msg?.ToString() ?? "");
        }

        [GoMethod(IsVariadic = true)]
        public void Skipf(string format, params object?[] args)
        {
            _skipped = true;
            Logf(format, args);
            throw new TestSkipException(_logs[_logs.Count - 1]);
        }

        public void SkipNow()
        {
            _skipped = true;
            throw new TestSkipException("SkipNow called");
        }

        public void Helper()
        {
            // No-op in our implementation — Go uses this for stack trace filtering
        }

        [GoMethod]
        public void Cleanup(Action f)
        {
            _cleanups.Add(f);
        }

        [GoMethod]
        public void Setenv(string key, string value)
        {
            var prev = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
            Cleanup(() => Environment.SetEnvironmentVariable(key, prev));
        }

        public string TempDir()
        {
            var dir = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "ngo-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            Cleanup(() =>
            {
                try { Directory.Delete(dir, true); } catch { }
            });
            return dir;
        }

        public bool Run(string name, Action<T> f)
        {
            var sub = new T(_name + "/" + name);
            try
            {
                f(sub);
            }
            catch (TestFailException)
            {
                // Already marked as failed
            }
            catch (TestSkipException)
            {
                // Already marked as skipped
            }
            finally
            {
                sub.RunCleanups();
            }

            if (sub._failed)
            {
                _failed = true;
            }

            return !sub._failed;
        }

        public void RunCleanups()
        {
            for (int i = _cleanups.Count - 1; i >= 0; i--)
            {
                try { _cleanups[i](); } catch { }
            }
        }

        public IReadOnlyList<string> GetLogs()
        {
            return _logs;
        }
    }
}
