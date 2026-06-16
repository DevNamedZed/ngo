// -----------------------------------------------------------------------
// <copyright file="EmitSession.cs" company="Ziad">
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

using System.Reflection.Emit;
using Ngo.Compiler.Emit.Builder;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Root of the emit-time ownership tree — one per build. Owns the shared emit state (the
    /// <see cref="TypeMapper"/> and the <see cref="EmitContext"/>) and is the point from which
    /// the per-package/type/method contexts are created (spec/F4-EMIT-CONTEXT-HIERARCHY.md).
    ///
    /// It holds only managed state, so it needs no disposal: when the session goes out of scope
    /// the GC reclaims it and everything it owns. The emitted assembly is collectible
    /// (RunAndCollect) and is unloaded by the GC once unreferenced — nothing here to dispose.
    /// </summary>
    internal sealed class EmitSession
    {
        public EmitContext Context { get; }

        public TypeMapper Mapper { get; }

        public EmitSession(ModuleBuilder moduleBuilder, EmitOptions? options,
            Semantics.CompilationContext compilationContext)
        {
            Mapper = new TypeMapper(compilationContext);
            Context = new EmitContext(new LiveModuleBuilder(moduleBuilder), Mapper, options, compilationContext.Log);
            Mapper.SetEmitContext(Context);
        }
    }
}
