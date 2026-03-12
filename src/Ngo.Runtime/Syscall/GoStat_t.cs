// -----------------------------------------------------------------------
// <copyright file="GoStat_t.cs" company="Ziad">
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
    [GoType("struct", Name = "Stat_t", Package = "syscall")]
    public class GoStat_t
    {
        [GoField] public long Dev;
        [GoField] public long Ino;
        [GoField] public long Nlink;
        [GoField] public long Mode;
        [GoField] public long Uid;
        [GoField] public long Gid;
        [GoField] public long Rdev;
        [GoField] public long Size;
        [GoField] public long Blksize;
        [GoField] public long Blocks;
        [GoField] public GoTimespec Atim;
        [GoField] public GoTimespec Mtim;
        [GoField] public GoTimespec Ctim;
    }
}
