// -----------------------------------------------------------------------
// <copyright file="GoReflectMethod.cs" company="Ziad">
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

namespace Ngo.Runtime.Reflect
{
    // reflect.Method struct
    [GoType("struct", Name = "Method", Package = "reflect")]
    public class GoReflectMethod
    {
        [GoField(Name = "Name")] public string Name { get; set; } = "";
        [GoField(Name = "PkgPath")] public string PkgPath { get; set; } = "";
        [GoField(Name = "Type")] public GoReflectType? Type { get; set; }
        [GoField(Name = "Func")] public GoReflectValue? Func { get; set; }
        [GoField(Name = "Index")] public long Index { get; set; }

        [GoMethod]
        public bool IsExported() => Name.Length > 0 && char.IsUpper(Name[0]);
    }
}
