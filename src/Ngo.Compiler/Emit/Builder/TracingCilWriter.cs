// -----------------------------------------------------------------------
// <copyright file="TracingCilWriter.cs" company="Ziad">
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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Emit.Refs;

namespace Ngo.Compiler.Emit.Builder
{
    /// <summary>
    /// Decorator around CilWriter that records every IL instruction emitted,
    /// then forwards to the inner writer. Used for diagnosing invalid IL.
    /// </summary>
    internal sealed class TracingCilWriter : CilWriter
    {
        private readonly CilWriter _inner;
        private readonly List<string> _trace = new();
        private int _instructionIndex;

        public TracingCilWriter(CilWriter inner)
        {
            _inner = inner;
        }

        public IReadOnlyList<string> Trace => _trace;

        private void Log(string entry)
        {
            _trace.Add($"  IL_{_instructionIndex:X4}: {entry}");
            _instructionIndex++;
        }

        public override void Emit(OpCode op)
        {
            Log(op.Name ?? op.ToString());
            _inner.Emit(op);
        }

        public override void Emit(OpCode op, int arg)
        {
            Log($"{op.Name} {arg}");
            _inner.Emit(op, arg);
        }

        public override void Emit(OpCode op, long arg)
        {
            Log($"{op.Name} {arg}L");
            _inner.Emit(op, arg);
        }

        public override void Emit(OpCode op, float arg)
        {
            Log($"{op.Name} {arg}f");
            _inner.Emit(op, arg);
        }

        public override void Emit(OpCode op, double arg)
        {
            Log($"{op.Name} {arg}d");
            _inner.Emit(op, arg);
        }

        public override void Emit(OpCode op, string arg)
        {
            var truncated = arg.Length > 40 ? arg.Substring(0, 40) + "..." : arg;
            Log($"{op.Name} \"{truncated}\"");
            _inner.Emit(op, arg);
        }

        public override void Emit(OpCode op, byte arg)
        {
            Log($"{op.Name} {arg}");
            _inner.Emit(op, arg);
        }

        public override void Emit(OpCode op, Type type)
        {
            Log($"{op.Name} [{type.FullName ?? type.Name}]");
            _inner.Emit(op, type);
        }

        public override void Emit(OpCode op, MethodInfo method)
        {
            var declaringName = method.DeclaringType?.Name ?? "?";
            string genericSuffix = "";
            if (method.IsGenericMethod)
            {
                try
                {
                    var args = method.GetGenericArguments();
                    var argNames = new string[args.Length];
                    for (int index = 0; index < args.Length; index++)
                    {
                        argNames[index] = args[index].Name;
                    }
                    genericSuffix = $"<{string.Join(",", argNames)}>";
                }
                catch
                {
                    genericSuffix = "<...>";
                }
            }
            string paramSuffix = "";
            try
            {
                var parameters = method.GetParameters();
                if (parameters.Length > 0)
                {
                    var paramTypeNames = new string[parameters.Length];
                    for (int pi = 0; pi < parameters.Length; pi++)
                    {
                        paramTypeNames[pi] = parameters[pi].ParameterType?.Name ?? "?";
                    }
                    paramSuffix = $"({string.Join(", ", paramTypeNames)})";
                }
            }
            catch
            {
                paramSuffix = "(...)";
            }
            Log($"{op.Name} {declaringName}::{method.Name}{genericSuffix}{paramSuffix}");
            _inner.Emit(op, method);
        }

        public override void Emit(OpCode op, ConstructorInfo ctor)
        {
            var declaringName = ctor.DeclaringType?.FullName ?? ctor.DeclaringType?.Name ?? "?";
            Log($"{op.Name} {declaringName}::.ctor");
            _inner.Emit(op, ctor);
        }

        public override void Emit(OpCode op, FieldInfo field)
        {
            var declaringType = field.DeclaringType;
            string declaringName;
            if (declaringType != null && declaringType.IsGenericType)
            {
                try
                {
                    var args = declaringType.GetGenericArguments();
                    var argNames = new string[args.Length];
                    for (int index = 0; index < args.Length; index++)
                    {
                        argNames[index] = args[index].Name;
                    }
                    declaringName = $"{declaringType.Name}<{string.Join(",", argNames)}>";
                }
                catch
                {
                    declaringName = declaringType.FullName ?? declaringType.Name;
                }
            }
            else
            {
                declaringName = declaringType?.Name ?? "?";
            }
            Log($"{op.Name} {declaringName}::{field.Name} (fieldType={field.FieldType?.Name ?? "?"})");
            _inner.Emit(op, field);
        }

        public override void Emit(OpCode op, TypeRef typeRef)
        {
            Log($"{op.Name} [{typeRef.DisplayName}]");
            _inner.Emit(op, typeRef);
        }

        public override void Emit(OpCode op, MethodRef methodRef)
        {
            Log($"{op.Name} {methodRef}");
            _inner.Emit(op, methodRef);
        }

        public override void Emit(OpCode op, CtorRef ctorRef)
        {
            Log($"{op.Name} {ctorRef}");
            _inner.Emit(op, ctorRef);
        }

        public override void Emit(OpCode op, FieldRef fieldRef)
        {
            Log($"{op.Name} {fieldRef}");
            _inner.Emit(op, fieldRef);
        }

        public override void Emit(OpCode op, Label label)
        {
            Log($"{op.Name} label#{label.GetHashCode()}");
            _inner.Emit(op, label);
        }

        public override void Emit(OpCode op, Label[] labels)
        {
            Log($"{op.Name} [{labels.Length} labels]");
            _inner.Emit(op, labels);
        }

        public override void Emit(OpCode op, LocalBuilder local)
        {
            Log($"{op.Name} local_{local.LocalIndex} ({local.LocalType?.Name ?? "?"})");
            _inner.Emit(op, local);
        }

        public override LocalBuilder DeclareLocal(Type type)
        {
            var local = _inner.DeclareLocal(type);
            _trace.Add($"  .locals: [{local.LocalIndex}] {type.FullName ?? type.Name}");
            return local;
        }

        public override Label DefineLabel()
        {
            var label = _inner.DefineLabel();
            _trace.Add($"  .label: defined label#{label.GetHashCode()}");
            return label;
        }

        public override void MarkLabel(Label label)
        {
            _trace.Add($"  label#{label.GetHashCode()}:");
            _inner.MarkLabel(label);
        }

        public override void BeginExceptionBlock()
        {
            _trace.Add("  .try {");
            _inner.BeginExceptionBlock();
        }

        public override void BeginCatchBlock(Type type)
        {
            _trace.Add($"  }} catch ({type.FullName ?? type.Name}) {{");
            _inner.BeginCatchBlock(type);
        }

        public override void BeginFinallyBlock()
        {
            _trace.Add("  } finally {");
            _inner.BeginFinallyBlock();
        }

        public override void BeginFaultBlock()
        {
            _trace.Add("  } fault {");
            _inner.BeginFaultBlock();
        }

        public override void BeginExceptFilterBlock()
        {
            _trace.Add("  } filter {");
            _inner.BeginExceptFilterBlock();
        }

        public override void EndExceptionBlock()
        {
            _trace.Add("  }");
            _inner.EndExceptionBlock();
        }
    }
}
