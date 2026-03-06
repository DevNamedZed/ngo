// -----------------------------------------------------------------------
// <copyright file="GoTesting.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;

namespace Ngo.Runtime
{
    public class GoTestingT
    {
        private readonly string _name;
        private bool _failed;
        private bool _skipped;
        private readonly List<Action> _cleanups = new();
        private readonly List<string> _logs = new();

        public GoTestingT(string name)
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

        public void Log(object? msg)
        {
            _logs.Add(msg?.ToString() ?? "");
        }

        public void Logf(string format, object? args)
        {
            var msg = Fmt.Sprintf(format, args is object?[] arr ? arr : new object?[] { args });
            _logs.Add(msg);
        }

        public void Error(object? msg)
        {
            _failed = true;
            Log(msg);
        }

        public void Errorf(string format, object? args)
        {
            _failed = true;
            Logf(format, args);
        }

        public void Fatal(object? msg)
        {
            _failed = true;
            Log(msg);
            throw new GoTestFailException(_logs[_logs.Count - 1]);
        }

        public void Fatalf(string format, object? args)
        {
            _failed = true;
            Logf(format, args);
            throw new GoTestFailException(_logs[_logs.Count - 1]);
        }

        public void Fail()
        {
            _failed = true;
        }

        public void FailNow()
        {
            _failed = true;
            throw new GoTestFailException("FailNow called");
        }

        public void Skip(object? msg)
        {
            _skipped = true;
            Log(msg);
            throw new GoTestSkipException(msg?.ToString() ?? "");
        }

        public void Skipf(string format, object? args)
        {
            _skipped = true;
            Logf(format, args);
            throw new GoTestSkipException(_logs[_logs.Count - 1]);
        }

        public void SkipNow()
        {
            _skipped = true;
            throw new GoTestSkipException("SkipNow called");
        }

        public void Helper()
        {
            // No-op in our implementation — Go uses this for stack trace filtering
        }

        public void Cleanup(Action f)
        {
            _cleanups.Add(f);
        }

        public string TempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ngo-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            Cleanup(() =>
            {
                try { Directory.Delete(dir, true); } catch { }
            });
            return dir;
        }

        public bool Run(string name, Action<GoTestingT> f)
        {
            var sub = new GoTestingT(_name + "/" + name);
            try
            {
                f(sub);
            }
            catch (GoTestFailException)
            {
                // Already marked as failed
            }
            catch (GoTestSkipException)
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

    public class GoTestFailException : Exception
    {
        public GoTestFailException(string message) : base(message) { }
    }

    public class GoTestSkipException : Exception
    {
        public GoTestSkipException(string message) : base(message) { }
    }
}
