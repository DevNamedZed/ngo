// -----------------------------------------------------------------------
// <copyright file="LiveFieldBuilder.cs" company="Ziad">
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

using System.Reflection;
using System.Reflection.Emit;

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class LiveFieldBuilder : IFieldBuilder
    {
        private readonly FieldBuilder _fb;

        public LiveFieldBuilder(FieldBuilder fb) => _fb = fb;

        public FieldBuilder Inner => _fb;
        public FieldInfo AsFieldInfo() => _fb;

        public void SetCustomAttribute(CustomAttributeBuilder attr) => _fb.SetCustomAttribute(attr);
    }
}
