// -----------------------------------------------------------------------
// <copyright file="GoDirEntry.cs" company="Ziad">
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
    [GoType("struct", Name = "DirEntry", Package = "os")]
    public sealed class GoDirEntry
    {
        public string NameValue { get; }
        public bool IsDirValue { get; }

        public GoDirEntry(string name, bool isDir)
        {
            NameValue = name;
            IsDirValue = isDir;
        }

        [GoMethod]
        public string Name() => NameValue;
        [GoMethod]
        public bool IsDir() => IsDirValue;
        [GoMethod]
        [return: GoReturn("FileMode")]
        public long Type()
        {
            if (IsDirValue) return GoOs.ModeDir;
            return 0;
        }
        [GoMethod]
        [return: GoReturn("FileInfo", "error")]
        public (GoFileInfo, object?) Info()
        {
            return (new GoFileInfo(NameValue, 0, IsDirValue), null);
        }

        public override string ToString() => NameValue;
    }
}
