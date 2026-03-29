// -----------------------------------------------------------------------
// <copyright file="GoProcessState.cs" company="Ziad">
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

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os
{
    /// <summary>
    /// Represents Go's os.ProcessState struct.
    /// </summary>
    [GoType("struct", Name = "ProcessState", Package = "os")]
    public sealed class GoProcessState
    {
        private readonly int _exitCode;
        private readonly bool _exited;
        private readonly int _pid;
        private readonly System.TimeSpan _userTime;
        private readonly System.TimeSpan _systemTime;

        public static readonly GoProcessState Empty = new GoProcessState(0, false);

        public GoProcessState(int exitCode, bool exited, int pid = 0,
            System.TimeSpan userTime = default, System.TimeSpan systemTime = default)
        {
            _exitCode = exitCode;
            _exited = exited;
            _pid = pid;
            _userTime = userTime;
            _systemTime = systemTime;
        }

        [GoMethod]
        public long ExitCode() => _exitCode;
        [GoMethod]
        public bool Exited() => _exited;
        [GoMethod]
        public long Pid() => _pid;
        [GoMethod]
        public string String() => _exited ? $"exit status {_exitCode}" : "running";
        [GoMethod]
        public bool Success() => _exitCode == 0;
        [GoMethod]
        [return: GoReturn("interface{}")]
        public object? Sys()
        {
            var waitStatus = new Syscall.GoWaitStatus();
            if (_exited)
            {
                waitStatus.Value = (_exitCode & 0xff) << 8; // WEXITSTATUS encoding
            }
            return waitStatus;
        }
        [GoMethod]
        [return: GoReturn("interface{}")]
        public object? SysUsage()
        {
            var rusage = new Syscall.GoRusage();
            rusage.Utime = new Syscall.GoTimeval { Sec = _userTime.Ticks / System.TimeSpan.TicksPerSecond, Usec = (_userTime.Ticks % System.TimeSpan.TicksPerSecond) / 10 };
            rusage.Stime = new Syscall.GoTimeval { Sec = _systemTime.Ticks / System.TimeSpan.TicksPerSecond, Usec = (_systemTime.Ticks % System.TimeSpan.TicksPerSecond) / 10 };
            return rusage;
        }
        [GoMethod]
        [return: GoReturn("time.Duration")]
        public object? SystemTime() => _systemTime.Ticks * 100; // nanoseconds
        [GoMethod]
        [return: GoReturn("time.Duration")]
        public object? UserTime() => _userTime.Ticks * 100; // nanoseconds
    }
}
