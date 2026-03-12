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

        public static readonly GoProcessState Empty = new GoProcessState(0, false);

        public GoProcessState(int exitCode, bool exited)
        {
            _exitCode = exitCode;
            _exited = exited;
        }

        [GoMethod]
        public long ExitCode() => _exitCode;
        [GoMethod]
        public bool Exited() => _exited;
        [GoMethod]
        public long Pid() => 0;
        [GoMethod]
        public string String() => _exited ? $"exit status {_exitCode}" : "running";
        [GoMethod]
        public bool Success() => _exitCode == 0;
        [GoMethod]
        [return: GoReturn("interface{}")]
        public object? Sys() => null;
        [GoMethod]
        [return: GoReturn("interface{}")]
        public object? SysUsage() => null;
        [GoMethod]
        [return: GoReturn("interface{}")]
        public object? SystemTime() => null;
        [GoMethod]
        [return: GoReturn("interface{}")]
        public object? UserTime() => null;
    }
}
