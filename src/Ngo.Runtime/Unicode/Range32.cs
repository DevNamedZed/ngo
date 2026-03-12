// -----------------------------------------------------------------------
// <copyright file="Range32.cs" company="Ziad">
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

namespace Ngo.Runtime.Unicode
{
    [GoType("struct", Name = "Range32", Package = "unicode")]
    public struct Range32
    {
        [GoField(Name = "Lo")]
        public long Lo;
        [GoField(Name = "Hi")]
        public long Hi;
        [GoField(Name = "Stride")]
        public long Stride;
    }
}
