// -----------------------------------------------------------------------
// <copyright file="GoLocation.cs" company="Ziad">
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

namespace Ngo.Runtime.Time
{
    // time.Location — struct type
    [GoType("struct", Name = "Location", Package = "time")]
    public sealed class GoLocation
    {
        public string Name { get; }
        public TimeSpan Offset { get; }

        public GoLocation(string name, TimeSpan offset)
        {
            Name = name;
            Offset = offset;
        }

        [GoMethod]
        public string String() => Name;

        public override string ToString() => String();
    }
}
