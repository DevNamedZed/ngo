// -----------------------------------------------------------------------
// <copyright file="GoSysProcAttr.cs" company="Ziad">
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
    [GoType("struct", Name = "SysProcAttr", Package = "syscall")]
    public class GoSysProcAttr
    {
        [GoField] public string Chroot;
        [GoField] public object? Credential;
        [GoField] public bool Ptrace;
        [GoField] public bool Setsid;
        [GoField] public bool Setpgid;
        [GoField] public bool Setctty;
        [GoField] public bool Noctty;
        [GoField] public long Ctty;
        [GoField] public bool Foreground;
        [GoField] public long Pgid;
        [GoField] public long Pdeathsig;
        [GoField] public long Cloneflags;
        [GoField] public long Unshareflags;
        [GoField] public Slice<long> UidMappings;
        [GoField] public Slice<long> GidMappings;
        [GoField] public bool GidMappingsEnableSetgroups;
        [GoField] public Slice<long> AmbientCaps;
    }
}
