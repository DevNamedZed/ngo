// -----------------------------------------------------------------------
// <copyright file="ILGeneratorWriter.cs" company="Ziad">
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
using System.Reflection;
using System.Reflection.Emit;

namespace Ngo.Compiler.Emit.Builder
{
    /// <summary>
    /// CilWriter that forwards all calls to a real ILGenerator.
    /// Used for main package emission (ngo run / ngo build).
    /// </summary>
    internal sealed class ILGeneratorWriter : CilWriter
    {
        private readonly ILGenerator _il;

        public ILGeneratorWriter(ILGenerator il) => _il = il;

        public override void Emit(OpCode op) => _il.Emit(op);
        public override void Emit(OpCode op, int arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, long arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, float arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, double arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, string arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, byte arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, Type type) => _il.Emit(op, type);
        public override void Emit(OpCode op, MethodInfo method) => _il.Emit(op, method);
        public override void Emit(OpCode op, ConstructorInfo ctor) => _il.Emit(op, ctor);
        public override void Emit(OpCode op, FieldInfo field) => _il.Emit(op, field);
        public override void Emit(OpCode op, Label label) => _il.Emit(op, label);
        public override void Emit(OpCode op, Label[] labels) => _il.Emit(op, labels);
        public override void Emit(OpCode op, LocalBuilder local) => _il.Emit(op, local);

        public override LocalBuilder DeclareLocal(Type type) => _il.DeclareLocal(type);
        public override Label DefineLabel() => _il.DefineLabel();
        public override void MarkLabel(Label label) => _il.MarkLabel(label);

        public override void BeginExceptionBlock() => _il.BeginExceptionBlock();
        public override void BeginCatchBlock(Type type) => _il.BeginCatchBlock(type);
        public override void EndExceptionBlock() => _il.EndExceptionBlock();
    }
}
