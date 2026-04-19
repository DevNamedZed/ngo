// -----------------------------------------------------------------------
// <copyright file="CilWriter.cs" company="Ziad">
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
using Ngo.Compiler.Emit.Refs;

namespace Ngo.Compiler.Emit.Builder
{
    /// <summary>
    /// Abstract IL emission target. MethodBodyEmitter emits through this.
    /// Two implementations: ILGeneratorWriter (real ILGenerator) and NgoWriter (.ngo archive buffer).
    /// Locals and labels are represented by domain types (LocalSlot, LabelSlot) so both writers
    /// can carry the original declared type through emission without round-tripping through a
    /// scratch ILGenerator.
    /// </summary>
    internal abstract class CilWriter
    {
        public abstract void Emit(OpCode op);
        public abstract void Emit(OpCode op, int arg);
        public abstract void Emit(OpCode op, long arg);
        public abstract void Emit(OpCode op, float arg);
        public abstract void Emit(OpCode op, double arg);
        public abstract void Emit(OpCode op, string arg);
        public abstract void Emit(OpCode op, byte arg);
        public abstract void Emit(OpCode op, Type type);
        public abstract void Emit(OpCode op, MethodInfo method);
        public abstract void Emit(OpCode op, ConstructorInfo ctor);
        public abstract void Emit(OpCode op, FieldInfo field);
        public abstract void Emit(OpCode op, LabelSlot label);
        public abstract void Emit(OpCode op, LabelSlot[] labels);
        public abstract void Emit(OpCode op, LocalSlot local);

        public abstract void Emit(OpCode op, TypeRef typeRef);
        public abstract void Emit(OpCode op, MethodRef methodRef);
        public abstract void Emit(OpCode op, CtorRef ctorRef);
        public abstract void Emit(OpCode op, FieldRef fieldRef);

        public abstract LocalSlot DeclareLocal(Type type);
        public abstract LabelSlot DefineLabel();
        public abstract void MarkLabel(LabelSlot label);

        public abstract void BeginExceptionBlock();
        public abstract void BeginCatchBlock(Type type);
        public abstract void BeginFinallyBlock();
        public abstract void BeginFaultBlock();
        public abstract void BeginExceptFilterBlock();
        public abstract void EndExceptionBlock();
    }
}
