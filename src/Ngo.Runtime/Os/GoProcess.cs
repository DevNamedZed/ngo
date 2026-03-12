// -----------------------------------------------------------------------
// <copyright file="GoProcess.cs" company="Ziad">
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
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os
{
    /// <summary>
    /// Represents Go's os.Process struct.
    /// </summary>
    [GoType("struct", Name = "Process", Package = "os")]
    public sealed class GoProcess
    {
        private readonly System.Diagnostics.Process? _proc;

        public static readonly GoProcess Null = new GoProcess(null);

        public GoProcess(System.Diagnostics.Process? proc)
        {
            _proc = proc;
        }

        [GoField(Name = "Pid")]
        public long Pid => _proc?.Id ?? 0;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Kill()
        {
            if (_proc == null) return "os: process not initialized";
            try { _proc.Kill(); return null; }
            catch (Exception ex) { return ex.Message; }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Signal([GoParam("Signal")] object sig)
        {
            return "os: Signal not supported on this platform";
        }

        [GoMethod]
        [return: GoReturn("*ProcessState", "error")]
        public (GoProcessState, object?) Wait()
        {
            if (_proc == null) return (GoProcessState.Empty, "os: process not initialized");
            try
            {
                _proc.WaitForExit();
                return (new GoProcessState(_proc.ExitCode, true), null);
            }
            catch (Exception ex)
            {
                return (GoProcessState.Empty, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Release()
        {
            _proc?.Dispose();
            return null;
        }
    }
}
