// -----------------------------------------------------------------------
// <copyright file="NgoWriter.cs" company="Ziad">
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
using Ngo.Compiler.Emit;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Emit.Builder;

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// CilWriter that captures IL into a byte buffer with symbolic token references.
    /// Produces the same binary format that LinkIL/ReplayIL expects (Sections 2+3).
    /// Used for dependency packages → .ngo archive.
    /// </summary>
    internal sealed class NgoWriter : CilWriter
    {
        private readonly MemoryStream _code = new();
        private readonly List<TokenData> _tokens = new();
        private readonly List<string> _locals = new();
        private readonly List<ExceptionClause> _exceptionClauses = new();

        // Label tracking
        private int _nextLabelId;
        private readonly Dictionary<int, int> _labelOffsets = new(); // labelId → code offset
        private readonly List<BranchFixup> _branchFixups = new();

        // Exception block tracking
        private readonly Stack<int> _tryStartOffsets = new();
        private int _currentTryStart = -1;
        private int _currentTryLength;
        private int _currentHandlerStart = -1;

        public byte[] GetILBytes()
        {
            PatchBranches();
            return _code.ToArray();
        }

        public string[] GetLocalTypes() => _locals.ToArray();
        public List<TokenData> GetTokenEntries() => _tokens;
        public List<ExceptionClause> GetExceptionClauses() => _exceptionClauses;

        // ----- Emit overloads -----

        public override void Emit(OpCode op)
        {
            WriteOpCode(op);
        }

        public override void Emit(OpCode op, int arg)
        {
            WriteOpCode(op);
            WriteInt32(arg);
        }

        public override void Emit(OpCode op, long arg)
        {
            WriteOpCode(op);
            WriteInt64(arg);
        }

        public override void Emit(OpCode op, float arg)
        {
            WriteOpCode(op);
            WriteSingle(arg);
        }

        public override void Emit(OpCode op, double arg)
        {
            WriteOpCode(op);
            WriteDouble(arg);
        }

        public override void Emit(OpCode op, string arg)
        {
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(new TokenData
            {
                Offset = offset,
                Kind = TokenKind.String,
                Reference = arg,
                MemberName = "",
                GenericTypeArgs = Array.Empty<string>(),
                ParamTypes = Array.Empty<string>(),
            });
            WriteInt32(0); // placeholder token
        }

        public override void Emit(OpCode op, byte arg)
        {
            WriteOpCode(op);
            _code.WriteByte(arg);
        }

        public override void Emit(OpCode op, Type type)
        {
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(new TokenData
            {
                Offset = offset,
                Kind = TokenKind.Type,
                Reference = GetTypeName(type),
                MemberName = "",
                GenericTypeArgs = Array.Empty<string>(),
                ParamTypes = Array.Empty<string>(),
            });
            WriteInt32(0); // placeholder token
        }

        public override void Emit(OpCode op, MethodInfo method)
        {
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(BuildMethodToken(offset, method));
            WriteInt32(0); // placeholder token
        }

        public override void Emit(OpCode op, ConstructorInfo ctor)
        {
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(BuildConstructorToken(offset, ctor));
            WriteInt32(0); // placeholder token
        }

        public override void Emit(OpCode op, FieldInfo field)
        {
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(new TokenData
            {
                Offset = offset,
                Kind = TokenKind.Field,
                Reference = GetTypeName(field.DeclaringType!),
                MemberName = field.Name,
                GenericTypeArgs = Array.Empty<string>(),
                ParamTypes = Array.Empty<string>(),
            });
            WriteInt32(0); // placeholder token
        }

        public override void Emit(OpCode op, Label label)
        {
            WriteOpCode(op);
            var fixupOffset = (int)_code.Position;
            var labelId = GetLabelId(label);

            if (IsShortBranch(op))
            {
                _branchFixups.Add(new BranchFixup { Offset = fixupOffset, LabelId = labelId, IsShort = true });
                _code.WriteByte(0); // 1-byte placeholder
            }
            else
            {
                _branchFixups.Add(new BranchFixup { Offset = fixupOffset, LabelId = labelId, IsShort = false });
                WriteInt32(0); // 4-byte placeholder
            }
        }

        public override void Emit(OpCode op, Label[] labels)
        {
            WriteOpCode(op);
            WriteInt32(labels.Length);
            int baseOffset = (int)_code.Position + labels.Length * 4;
            for (int i = 0; i < labels.Length; i++)
            {
                var fixupOffset = (int)_code.Position;
                var labelId = GetLabelId(labels[i]);
                _branchFixups.Add(new BranchFixup { Offset = fixupOffset, LabelId = labelId, IsShort = false, BaseOffset = baseOffset });
                WriteInt32(0); // placeholder
            }
        }

        public override void Emit(OpCode op, LocalBuilder local)
        {
            WriteOpCode(op);
            var operandType = op.OperandType;
            if (operandType == OperandType.ShortInlineVar)
            {
                _code.WriteByte((byte)local.LocalIndex);
            }
            else if (operandType == OperandType.InlineVar)
            {
                WriteInt16((short)local.LocalIndex);
            }
        }

        // ----- Label/Local support -----

        public override LocalBuilder DeclareLocal(Type type)
        {
            _locals.Add(GetTypeName(type));
            // Substitute non-runtime types (NgoProxyType etc.) with typeof(object)
            // since ILGenerator rejects TypeDelegator subclasses
            var safeType = type is TypeDelegator ? typeof(object) : type;
            try
            {
                return _localFactory.DeclareLocal(safeType);
            }
            catch (ArgumentException)
            {
                return _localFactory.DeclareLocal(typeof(object));
            }
        }

        public override Label DefineLabel()
        {
            var label = _labelFactory.DefineLabel();
            _nextLabelId++;
            return label;
        }

        public override void MarkLabel(Label label)
        {
            var labelId = GetLabelId(label);
            _labelOffsets[labelId] = (int)_code.Position;
        }

        // ----- Exception handling -----

        public override void BeginExceptionBlock()
        {
            _tryStartOffsets.Push((int)_code.Position);
        }

        public override void BeginCatchBlock(Type type)
        {
            if (_tryStartOffsets.Count > 0)
            {
                _currentTryStart = _tryStartOffsets.Peek();
                _currentTryLength = (int)_code.Position - _currentTryStart;
            }
            _currentHandlerStart = (int)_code.Position;

            _exceptionClauses.Add(new ExceptionClause
            {
                Kind = 0, // Catch
                TryOffset = _currentTryStart,
                TryLength = _currentTryLength,
                HandlerOffset = _currentHandlerStart,
                CatchTypeName = GetTypeName(type),
            });
        }

        public override void EndExceptionBlock()
        {
            if (_tryStartOffsets.Count > 0)
            {
                _tryStartOffsets.Pop();
            }

            if (_exceptionClauses.Count > 0)
            {
                var last = _exceptionClauses[_exceptionClauses.Count - 1];
                last.HandlerLength = (int)_code.Position - last.HandlerOffset;
                _exceptionClauses[_exceptionClauses.Count - 1] = last;
            }
        }

        // ----- Serialization to Section 3 format -----

        public void WriteMethodBody(BinaryWriter writer)
        {
            var ilBytes = GetILBytes();

            // MaxStack (ILGenerator calculates this automatically during replay)
            writer.Write(8);

            // Locals
            writer.Write(_locals.Count);
            foreach (var local in _locals)
            {
                writer.Write(local);
            }

            // IL bytes
            writer.Write(ilBytes.Length);
            writer.Write(ilBytes);

            // Token table — structured per kind
            writer.Write(_tokens.Count);
            foreach (var entry in _tokens)
            {
                writer.Write(entry.Offset);
                writer.Write(entry.Kind);
                switch (entry.Kind)
                {
                    case TokenKind.Type:
                    case TokenKind.String:
                        writer.Write(entry.Reference);
                        break;
                    case TokenKind.Field:
                        writer.Write(entry.Reference);
                        writer.Write(entry.MemberName);
                        break;
                    case TokenKind.Method:
                        writer.Write(entry.Reference);
                        writer.Write(entry.MemberName);
                        writer.Write(entry.GenericTypeArgs.Length);
                        foreach (var arg in entry.GenericTypeArgs)
                        {
                            writer.Write(arg);
                        }
                        writer.Write(entry.ParamTypes.Length);
                        foreach (var paramType in entry.ParamTypes)
                        {
                            writer.Write(paramType);
                        }
                        break;
                }
            }

            // Exception handlers
            writer.Write(_exceptionClauses.Count);
            foreach (var clause in _exceptionClauses)
            {
                writer.Write(clause.Kind);
                writer.Write(clause.TryOffset);
                writer.Write(clause.TryLength);
                writer.Write(clause.HandlerOffset);
                writer.Write(clause.HandlerLength);
                writer.Write(clause.FilterOffset);
                writer.Write(clause.CatchTypeName ?? "");
            }
        }

        public void Reset()
        {
            _code.SetLength(0);
            _code.Position = 0;
            _tokens.Clear();
            _locals.Clear();
            _exceptionClauses.Clear();
            _labelOffsets.Clear();
            _branchFixups.Clear();
            _nextLabelId = 0;
            _tryStartOffsets.Clear();
            _currentTryStart = -1;
            _currentHandlerStart = -1;
            _labelFactory = CreateFactory();
            _localFactory = CreateFactory();
        }

        // ----- Structured token builders -----

        private TokenData BuildMethodToken(int offset, MethodInfo method)
        {
            if (method == null)
            {
                throw new InvalidOperationException("NgoWriter: attempted to serialize a null method reference");
            }

            var declaringType = method.DeclaringType;
            var declaringTypeName = declaringType != null ? GetTypeName(declaringType) : "?";

            // For instantiated generic types, resolve generic params in parameter types
            // to their concrete type args. E.g., Slice<int>.Push(T) → Push(System.Int32)
            Type[]? declaringTypeArgs = null;
            Type[]? declaringTypeDefParams = null;
            if (declaringType != null && declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition)
            {
                declaringTypeArgs = declaringType.GetGenericArguments();
                declaringTypeDefParams = declaringType.GetGenericTypeDefinition().GetGenericArguments();
            }

            // Method-level generic type args for instantiated generic methods
            string[] genericTypeArgs;
            var methodGenericArgs = method.GetGenericArguments();
            if (methodGenericArgs.Length > 0 && method.IsGenericMethod && !method.IsGenericMethodDefinition)
            {
                genericTypeArgs = new string[methodGenericArgs.Length];
                for (int i = 0; i < methodGenericArgs.Length; i++)
                {
                    genericTypeArgs[i] = GetTypeName(methodGenericArgs[i]);
                }
            }
            else
            {
                genericTypeArgs = Array.Empty<string>();
            }

            var parameters = method.GetParameters();
            var paramTypes = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                if (paramType.IsGenericParameter && declaringTypeArgs != null && declaringTypeDefParams != null)
                {
                    for (int g = 0; g < declaringTypeDefParams.Length; g++)
                    {
                        if (declaringTypeDefParams[g] == paramType || declaringTypeDefParams[g].Name == paramType.Name)
                        {
                            paramType = declaringTypeArgs[g];
                            break;
                        }
                    }
                }
                paramTypes[i] = GetTypeName(paramType);
            }

            return new TokenData
            {
                Offset = offset,
                Kind = TokenKind.Method,
                Reference = declaringTypeName,
                MemberName = method.Name,
                GenericTypeArgs = genericTypeArgs,
                ParamTypes = paramTypes,
            };
        }

        private TokenData BuildConstructorToken(int offset, ConstructorInfo ctor)
        {
            if (ctor == null)
            {
                throw new InvalidOperationException("NgoWriter: attempted to serialize a null constructor reference");
            }

            var declaringType = ctor.DeclaringType;
            var parameters = ctor.GetParameters();
            var paramTypes = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                paramTypes[i] = GetTypeName(parameters[i].ParameterType);
            }

            return new TokenData
            {
                Offset = offset,
                Kind = TokenKind.Method,
                Reference = GetTypeName(declaringType!),
                MemberName = ".ctor",
                GenericTypeArgs = Array.Empty<string>(),
                ParamTypes = paramTypes,
            };
        }

        // ----- Private helpers -----

        private ILGenerator _labelFactory;
        private ILGenerator _localFactory;

        public NgoWriter()
        {
            _labelFactory = CreateFactory();
            _localFactory = CreateFactory();
        }

        private static ILGenerator CreateFactory()
        {
            var dm = new DynamicMethod("ngo_factory_" + Guid.NewGuid().ToString("N"), typeof(void), Type.EmptyTypes);
            return dm.GetILGenerator();
        }

        private static int GetLabelId(Label label)
        {
            return label.GetHashCode();
        }

        private void WriteOpCode(OpCode op)
        {
            if (op.Size == 2)
            {
                _code.WriteByte((byte)(op.Value >> 8));
                _code.WriteByte((byte)(op.Value & 0xFF));
            }
            else
            {
                _code.WriteByte((byte)op.Value);
            }
        }

        private void WriteInt16(short value)
        {
            _code.WriteByte((byte)(value & 0xFF));
            _code.WriteByte((byte)((value >> 8) & 0xFF));
        }

        private void WriteInt32(int value)
        {
            _code.WriteByte((byte)(value & 0xFF));
            _code.WriteByte((byte)((value >> 8) & 0xFF));
            _code.WriteByte((byte)((value >> 16) & 0xFF));
            _code.WriteByte((byte)((value >> 24) & 0xFF));
        }

        private void WriteInt64(long value)
        {
            for (int i = 0; i < 8; i++)
            {
                _code.WriteByte((byte)((value >> (i * 8)) & 0xFF));
            }
        }

        private void WriteSingle(float value)
        {
            var bytes = BitConverter.GetBytes(value);
            _code.Write(bytes, 0, 4);
        }

        private void WriteDouble(double value)
        {
            var bytes = BitConverter.GetBytes(value);
            _code.Write(bytes, 0, 8);
        }

        private void PatchBranches()
        {
            var buf = _code.GetBuffer();
            foreach (var fixup in _branchFixups)
            {
                if (!_labelOffsets.TryGetValue(fixup.LabelId, out var targetOffset))
                {
                    continue;
                }

                if (fixup.IsShort)
                {
                    var relOffset = targetOffset - (fixup.Offset + 1);
                    buf[fixup.Offset] = (byte)(sbyte)relOffset;
                }
                else
                {
                    var baseOff = fixup.BaseOffset > 0 ? fixup.BaseOffset : fixup.Offset + 4;
                    var relOffset = targetOffset - baseOff;
                    buf[fixup.Offset] = (byte)(relOffset & 0xFF);
                    buf[fixup.Offset + 1] = (byte)((relOffset >> 8) & 0xFF);
                    buf[fixup.Offset + 2] = (byte)((relOffset >> 16) & 0xFF);
                    buf[fixup.Offset + 3] = (byte)((relOffset >> 24) & 0xFF);
                }
            }
        }

        private static bool IsShortBranch(OpCode op)
        {
            return op.OperandType == OperandType.ShortInlineBrTarget;
        }

        internal static string GetTypeNameStatic(Type type) => GetTypeName(type);

        [ThreadStatic] private static HashSet<Type>? _typeNameInProgress;

        private static string GetTypeName(Type type)
        {
            _typeNameInProgress ??= new HashSet<Type>(ReferenceEqualityComparer.Instance);
            if (!_typeNameInProgress.Add(type))
            {
                return type.Name ?? "$$circular";
            }
            try
            {
                return GetTypeNameCore(type);
            }
            finally
            {
                _typeNameInProgress.Remove(type);
            }
        }

        private static string GetTypeNameCore(Type type)
        {
            if (type == typeof(void)) return "System.Void";
            if (type == typeof(object)) return "System.Object";
            if (type == typeof(string)) return "System.String";
            if (type == typeof(int)) return "System.Int32";
            if (type == typeof(long)) return "System.Int64";
            if (type == typeof(bool)) return "System.Boolean";
            if (type == typeof(byte)) return "System.Byte";
            if (type == typeof(short)) return "System.Int16";
            if (type == typeof(float)) return "System.Single";
            if (type == typeof(double)) return "System.Double";
            if (type == typeof(char)) return "System.Char";
            if (type == typeof(uint)) return "System.UInt32";
            if (type == typeof(ulong)) return "System.UInt64";
            if (type == typeof(ushort)) return "System.UInt16";
            if (type == typeof(sbyte)) return "System.SByte";
            if (type == typeof(nint)) return "System.IntPtr";
            if (type == typeof(nuint)) return "System.UIntPtr";

            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (type.IsArray)
            {
                return GetTypeName(type.GetElementType()!) + "[]";
            }

            if (type.IsByRef)
            {
                return GetTypeName(type.GetElementType()!) + "&";
            }

            if (type.IsPointer)
            {
                return GetTypeName(type.GetElementType()!) + "*";
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var genDef = type.GetGenericTypeDefinition();
                var args = type.GetGenericArguments();
                var argNames = string.Join(",", Array.ConvertAll(args, GetTypeName));
                return GetTypeName(genDef) + "[" + argNames + "]";
            }

            return type.FullName ?? type.Name;
        }

        // ----- Data types -----

        /// <summary>
        /// Internal structured token data captured during emission.
        /// Carries typed fields per token kind — no string formatting or parsing needed.
        /// </summary>
        internal struct TokenData
        {
            public int Offset;
            public byte Kind;
            public string Reference;
            public string MemberName;
            public string[] GenericTypeArgs;
            public string[] ParamTypes;
        }

        internal struct ExceptionClause
        {
            public int Kind;
            public int TryOffset;
            public int TryLength;
            public int HandlerOffset;
            public int HandlerLength;
            public int FilterOffset;
            public string? CatchTypeName;
        }

        private struct BranchFixup
        {
            public int Offset;
            public int LabelId;
            public bool IsShort;
            public int BaseOffset; // for switch targets
        }
    }
}
