// -----------------------------------------------------------------------
// <copyright file="GoWaitStatus.cs" company="Ziad">
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

namespace Ngo.Runtime.Syscall
{
    [GoType("named", Name = "WaitStatus", Package = "syscall", Underlying = "uint32")]
    public class GoWaitStatus
    {
        public long Value;

        [GoMethod]
        public bool Exited() => true;
        [GoMethod]
        public long ExitStatus() => 0;
        [GoMethod]
        public bool Signaled() => false;
        [GoMethod]
        [return: GoReturn("Signal")]
        public long Signal() => 0;
        [GoMethod]
        public bool CoreDump() => false;
        [GoMethod]
        public bool Stopped() => false;
        [GoMethod]
        public bool Continued() => false;
        [GoMethod]
        [return: GoReturn("Signal")]
        public long StopSignal() => 0;
        [GoMethod]
        public long TrapCause() => 0;
    }
}
