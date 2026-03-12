// -----------------------------------------------------------------------
// <copyright file="GoReflectStructField.cs" company="Ziad">
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
    /// <summary>
    /// Go reflect.StructTag named type (underlying string).
    /// </summary>
    [GoType("named", Name = "StructTag", Package = "reflect", Underlying = "string")]
    public class GoReflectStructTag
    {
        [GoMethod]
        public string Get(string key)
        {
            var (value, _) = Lookup(key);
            return value;
        }

        [GoMethod]
        public (string, bool) Lookup(string key)
        {
            return ("", false);
        }
    }

    /// <summary>
    /// Go reflect.StructField.
    /// </summary>
    [GoType("struct", Name = "StructField", Package = "reflect")]
    public sealed class GoReflectStructField
    {
        [GoField(Name = "Name")]
        public string Name { get; }
        [GoField(Name = "Type")]
        public GoReflectType Type { get; }
        [GoField(Name = "Tag", Type = "StructTag")]
        public string Tag { get; }
        [GoField(Name = "Index", Type = "[]int")]
        public Slice<long> Index { get; }
        [GoField(Name = "Anonymous")]
        public bool Anonymous { get; }
        [GoField(Name = "PkgPath")]
        public string PkgPath { get; } = "";

        internal GoReflectStructField(string name, GoReflectType type, string tag, int index, bool anonymous)
        {
            Name = name;
            Type = type;
            Tag = tag;
            Index = new Slice<long>(new long[] { index });
            Anonymous = anonymous;
        }

        [GoMethod]
        public bool IsExported() => Name.Length > 0 && char.IsUpper(Name[0]);

        public override string ToString() => $"{Name} {Type}";
    }
}
