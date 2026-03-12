// -----------------------------------------------------------------------
// <copyright file="GoSysinfo_t.cs" company="Ziad">
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
    [GoType("struct", Name = "Sysinfo_t", Package = "syscall")]
    public class GoSysinfo_t
    {
        [GoField] public long Uptime;
        [GoField] public long Totalram;
        [GoField] public long Freeram;
        [GoField] public long Sharedram;
        [GoField] public long Bufferram;
        [GoField] public long Totalswap;
        [GoField] public long Freeswap;
        [GoField] public long Procs;
        [GoField] public long Unit;
    }
}
