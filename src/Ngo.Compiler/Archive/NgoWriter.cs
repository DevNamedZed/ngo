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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Emit;
using Ngo.Compiler.Emit.Builder;
using Ngo.Runtime;
using Ngo.Runtime.Discovery;

namespace Ngo.Compiler.Archive
{
    internal sealed class NgoWriter : CilWriter
    {
        private readonly MemoryStream _code = new();
        private readonly List<ILTokenEntry> _tokens = new();
        private readonly List<string> _locals = new();
        private readonly List<ExceptionClause> _exceptionClauses = new();

        private int _nextLabelId;
        private readonly Dictionary<int, int> _labelOffsets = new();
        private readonly List<BranchFixup> _branchFixups = new();

        private int _currentStackDepth = 0;
        private int _maxStackDepth = 0;

        private readonly Stack<int> _tryStartOffsets = new();
        // Maps each try-block start offset to the try-length captured when the first handler began.
        // Keyed on try-start offset so nested blocks and multiple handlers on the same block
        // all get the correct TryLength regardless of order.
        private readonly Dictionary<int, int> _tryLengths = new();
        private int _currentHandlerStart = -1;

        private ILGenerator _labelFactory;
        private ILGenerator _localFactory;

        private static readonly Dictionary<Type, PrimitiveTypeKind> PrimitiveTypeMap = new()
        {
            { typeof(void), PrimitiveTypeKind.Void },
            { typeof(bool), PrimitiveTypeKind.Bool },
            { typeof(byte), PrimitiveTypeKind.Byte },
            { typeof(sbyte), PrimitiveTypeKind.SByte },
            { typeof(short), PrimitiveTypeKind.Int16 },
            { typeof(ushort), PrimitiveTypeKind.UInt16 },
            { typeof(int), PrimitiveTypeKind.Int32 },
            { typeof(uint), PrimitiveTypeKind.UInt32 },
            { typeof(long), PrimitiveTypeKind.Int64 },
            { typeof(ulong), PrimitiveTypeKind.UInt64 },
            { typeof(float), PrimitiveTypeKind.Float32 },
            { typeof(double), PrimitiveTypeKind.Float64 },
            { typeof(GoString), PrimitiveTypeKind.String },
            { typeof(object), PrimitiveTypeKind.Object },
            { typeof(nint), PrimitiveTypeKind.IntPtr },
            { typeof(nuint), PrimitiveTypeKind.UIntPtr },
            { typeof(char), PrimitiveTypeKind.Char },
        };

        private readonly SerializationContext _serializationContext;

        public NgoWriter(SerializationContext? serializationContext = null)
        {
            _serializationContext = serializationContext ?? SerializationContext.Empty;
            _labelFactory = CreateFactory();
            _localFactory = CreateFactory();
        }

        public byte[] GetILBytes()
        {
            PatchBranches();
            return _code.ToArray();
        }

        public string[] GetLocalTypes() => _locals.ToArray();
        public List<ILTokenEntry> GetTokenEntries() => _tokens;
        public List<ExceptionClause> GetExceptionClauses() => _exceptionClauses;

        // ----- Emit overloads -----

        public override void Emit(OpCode op)
        {
            TrackStack(op);
            WriteOpCode(op);
        }

        public override void Emit(OpCode op, int arg)
        {
            TrackStack(op);
            WriteOpCode(op);
            WriteInt32(arg);
        }

        public override void Emit(OpCode op, long arg)
        {
            TrackStack(op);
            WriteOpCode(op);
            WriteInt64(arg);
        }

        public override void Emit(OpCode op, float arg)
        {
            TrackStack(op);
            WriteOpCode(op);
            WriteSingle(arg);
        }

        public override void Emit(OpCode op, double arg)
        {
            TrackStack(op);
            WriteOpCode(op);
            WriteDouble(arg);
        }

        public override void Emit(OpCode op, string arg)
        {
            TrackStack(op);
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(ILTokenEntry.CreateString(offset, arg));
            WriteInt32(0);
        }

        public override void Emit(OpCode op, byte arg)
        {
            TrackStack(op);
            WriteOpCode(op);
            _code.WriteByte(arg);
        }

        public override void Emit(OpCode op, Type type)
        {
            TrackStack(op);
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(ILTokenEntry.CreateType(offset, BuildTypeToken(type)));
            WriteInt32(0);
        }

        public override void Emit(OpCode op, MethodInfo method)
        {
            // For call/callvirt the stack effect depends on the method signature:
            // push 1 if non-void return, pop N args + 1 if instance call.
            int push = method.ReturnType != typeof(void) ? 1 : 0;
            int pop = method.GetParameters().Length;
            // Use Attributes flag instead of IsStatic property — MethodBuilder throws
            // NotSupportedException from IsStatic before the type is created.
            bool isStatic = (method.Attributes & MethodAttributes.Static) != 0;
            if (!isStatic) pop += 1; // 'this'
            TrackStack(op, extraPush: push, extraPop: pop);
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(ILTokenEntry.CreateMethod(offset, BuildMethodToken(method)));
            WriteInt32(0);
        }

        public override void Emit(OpCode op, ConstructorInfo constructor)
        {
            // newobj pushes 1 (the new object), pops N args (no 'this' for newobj).
            // call .ctor pops N args + 'this', pushes nothing.
            int push = op == OpCodes.Newobj ? 1 : 0;
            int pop = constructor.GetParameters().Length + (op == OpCodes.Newobj ? 0 : 1);
            TrackStack(op, extraPush: push, extraPop: pop);
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(ILTokenEntry.CreateMethod(offset, BuildConstructorToken(constructor)));
            WriteInt32(0);
        }

        public override void Emit(OpCode op, FieldInfo field)
        {
            TrackStack(op);
            WriteOpCode(op);
            var offset = (int)_code.Position;
            _tokens.Add(ILTokenEntry.CreateField(offset, BuildFieldToken(field)));
            WriteInt32(0);
        }

        public override void Emit(OpCode op, Label label)
        {
            TrackStack(op);
            WriteOpCode(op);
            var fixupOffset = (int)_code.Position;
            var labelId = GetLabelId(label);

            if (IsShortBranch(op))
            {
                _branchFixups.Add(new BranchFixup { Offset = fixupOffset, LabelId = labelId, IsShort = true });
                _code.WriteByte(0);
            }
            else
            {
                _branchFixups.Add(new BranchFixup { Offset = fixupOffset, LabelId = labelId, IsShort = false });
                WriteInt32(0);
            }
        }

        public override void Emit(OpCode op, Label[] labels)
        {
            TrackStack(op);
            WriteOpCode(op);
            WriteInt32(labels.Length);
            int baseOffset = (int)_code.Position + labels.Length * 4;
            for (int index = 0; index < labels.Length; index++)
            {
                var fixupOffset = (int)_code.Position;
                var labelId = GetLabelId(labels[index]);
                _branchFixups.Add(new BranchFixup { Offset = fixupOffset, LabelId = labelId, IsShort = false, BaseOffset = baseOffset });
                WriteInt32(0);
            }
        }

        public override void Emit(OpCode op, LocalBuilder local)
        {
            TrackStack(op);
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
            _locals.Add(GetTypeNameStatic(type));

            var declaredType = type is TypeDelegator ? typeof(object) : type;
            try
            {
                return _localFactory.DeclareLocal(declaredType);
            }
            catch (ArgumentException)
            {
                // Some proxy / delegating types cannot be declared directly on the scratch ILGenerator.
                // Keep the serialized local type name intact, but use object for the temporary builder slot.
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
            int tryStart = _tryStartOffsets.Count > 0 ? _tryStartOffsets.Peek() : -1;

            // Capture the try-length exactly once per try-block (when the first handler fires).
            // Subsequent catch blocks on the same try must reuse the same length so that all
            // ExceptionClause entries for a given try share a consistent TryOffset/TryLength pair.
            if (tryStart >= 0 && !_tryLengths.ContainsKey(tryStart))
            {
                _tryLengths[tryStart] = (int)_code.Position - tryStart;
            }

            int tryLength = tryStart >= 0 && _tryLengths.TryGetValue(tryStart, out var len) ? len : 0;
            _currentHandlerStart = (int)_code.Position;

            // The CLR places the caught exception object on the stack at handler entry.
            // Reset depth to 1 so subsequent tracking starts from the correct baseline.
            _currentStackDepth = 1;
            if (_currentStackDepth > _maxStackDepth) _maxStackDepth = _currentStackDepth;

            _exceptionClauses.Add(new ExceptionClause
            {
                Kind = 0,
                TryOffset = tryStart,
                TryLength = tryLength,
                HandlerOffset = _currentHandlerStart,
                CatchTypeName = GetTypeNameStatic(type),
            });
        }

        public override void EndExceptionBlock()
        {
            int tryStart = -1;
            if (_tryStartOffsets.Count > 0)
            {
                tryStart = _tryStartOffsets.Pop();
            }

            // Patch the HandlerLength of every clause that belongs to this try-block.
            // (All handlers for a given try share the same TryOffset.)
            int endOffset = (int)_code.Position;
            for (int i = 0; i < _exceptionClauses.Count; i++)
            {
                var clause = _exceptionClauses[i];
                if (clause.TryOffset == tryStart && clause.HandlerLength == 0)
                {
                    clause.HandlerLength = endOffset - clause.HandlerOffset;
                    _exceptionClauses[i] = clause;
                }
            }

            // Release the cached try-length so the key doesn't linger for nested/re-used offsets.
            if (tryStart >= 0)
            {
                _tryLengths.Remove(tryStart);
            }
        }

        public override void BeginFinallyBlock()
        {
            int tryStart = _tryStartOffsets.Count > 0 ? _tryStartOffsets.Peek() : -1;
            if (tryStart >= 0 && !_tryLengths.ContainsKey(tryStart))
            {
                _tryLengths[tryStart] = (int)_code.Position - tryStart;
            }
            int tryLength = tryStart >= 0 && _tryLengths.TryGetValue(tryStart, out var len) ? len : 0;
            _currentHandlerStart = (int)_code.Position;
            // Finally blocks execute with an empty stack (no exception object).
            _currentStackDepth = 0;

            _exceptionClauses.Add(new ExceptionClause
            {
                Kind = 2, // Finally = 2 in ExceptionRegionKind
                TryOffset = tryStart,
                TryLength = tryLength,
                HandlerOffset = _currentHandlerStart,
                CatchTypeName = null,
            });
        }

        public override void BeginFaultBlock()
        {
            int tryStart = _tryStartOffsets.Count > 0 ? _tryStartOffsets.Peek() : -1;
            if (tryStart >= 0 && !_tryLengths.ContainsKey(tryStart))
            {
                _tryLengths[tryStart] = (int)_code.Position - tryStart;
            }
            int tryLength = tryStart >= 0 && _tryLengths.TryGetValue(tryStart, out var len) ? len : 0;
            _currentHandlerStart = (int)_code.Position;
            _currentStackDepth = 0;

            _exceptionClauses.Add(new ExceptionClause
            {
                Kind = 4, // Fault = 4 in ExceptionRegionKind
                TryOffset = tryStart,
                TryLength = tryLength,
                HandlerOffset = _currentHandlerStart,
                CatchTypeName = null,
            });
        }

        public override void BeginExceptFilterBlock()
        {
            int tryStart = _tryStartOffsets.Count > 0 ? _tryStartOffsets.Peek() : -1;
            if (tryStart >= 0 && !_tryLengths.ContainsKey(tryStart))
            {
                _tryLengths[tryStart] = (int)_code.Position - tryStart;
            }
            int tryLength = tryStart >= 0 && _tryLengths.TryGetValue(tryStart, out var len) ? len : 0;
            _currentHandlerStart = (int)_code.Position;
            // Filter blocks start with the exception object on the stack.
            _currentStackDepth = 1;
            if (_currentStackDepth > _maxStackDepth) _maxStackDepth = _currentStackDepth;

            _exceptionClauses.Add(new ExceptionClause
            {
                Kind = 1, // Filter = 1 in ExceptionRegionKind
                TryOffset = tryStart,
                TryLength = tryLength,
                HandlerOffset = _currentHandlerStart,
                FilterOffset = (int)_code.Position,
                CatchTypeName = null,
            });
        }

        // ----- Serialization to Section 3 format -----

        public void WriteMethodBody(BinaryWriter writer)
        {
            var ilBytes = GetILBytes();

            writer.Write(_maxStackDepth > 0 ? _maxStackDepth : 8); // actual tracked max stack

            writer.Write(_locals.Count);
            foreach (var local in _locals)
            {
                writer.Write(local);
            }

            writer.Write(ilBytes.Length);
            writer.Write(ilBytes);

            writer.Write(_tokens.Count);
            foreach (var entry in _tokens)
            {
                entry.Write(writer);
            }

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

        private TypeToken BuildTypeToken(Type type)
        {
            return BuildTypeToken(type, new HashSet<Type>(ReferenceEqualityComparer.Instance));
        }

        private TypeToken BuildTypeToken(Type type, HashSet<Type> inProgress)
        {
            if (!inProgress.Add(type))
            {
                return TypeToken.CreateTypeDef(type.FullName ?? type.Name);
            }

            try
            {
                return BuildTypeTokenCore(type, inProgress);
            }
            finally
            {
                inProgress.Remove(type);
            }
        }

        private bool TryBuildScopedGenericParameterToken(Type type, out TypeToken token)
        {
            int methodIndex = _serializationContext.FindMethodGenericParamIndex(type);
            if (methodIndex >= 0)
            {
                token = TypeToken.CreateGenericMethodParam(methodIndex);
                return true;
            }

            int typeIndex = _serializationContext.FindTypeGenericParamIndex(type);
            if (typeIndex >= 0)
            {
                token = TypeToken.CreateGenericTypeParam(typeIndex);
                return true;
            }

            token = null!;
            return false;
        }

        private static Type GetRequiredElementType(Type type)
        {
            return type.GetElementType()
                ?? throw new InvalidOperationException($"NgoWriter: '{type}' is missing an element type");
        }

        private static InvalidOperationException CreateMissingGenericParameterException(Type type)
        {
            return new InvalidOperationException(
                $"NgoWriter: generic parameter '{type.Name}' was not found in the current serialization context");
        }

        private TypeToken BuildTypeTokenCore(Type type, HashSet<Type> inProgress)
        {
            if (TryBuildScopedGenericParameterToken(type, out var scopedGenericToken))
            {
                return scopedGenericToken;
            }

            if (type is NgoProxyType proxyType)
            {
                if (proxyType.IsGenericParam)
                {
                    throw CreateMissingGenericParameterException(type);
                }

                if (type.IsArray)
                {
                    return TypeToken.CreateArray(BuildTypeToken(GetRequiredElementType(type), inProgress));
                }

                if (type.IsPointer)
                {
                    return TypeToken.CreatePointer(BuildTypeToken(GetRequiredElementType(type), inProgress));
                }

                if (type.IsByRef)
                {
                    return TypeToken.CreateByRef(BuildTypeToken(GetRequiredElementType(type), inProgress));
                }

                if (type.IsGenericType && !type.IsGenericTypeDefinition)
                {
                    var genericDefinition = type.GetGenericTypeDefinition();
                    var genericArguments = type.GetGenericArguments();
                    var argumentTokens = genericArguments.Select(arg => BuildTypeToken(arg, inProgress)).ToArray();
                    return TypeToken.CreateGenericInst(BuildTypeToken(genericDefinition, inProgress), argumentTokens);
                }

                return TypeToken.CreateTypeDef(type.FullName ?? type.Name);
            }

            if (PrimitiveTypeMap.TryGetValue(type, out var primitiveKind))
            {
                return TypeToken.CreatePrimitive(primitiveKind);
            }

            if (type.IsGenericParameter)
            {
                throw CreateMissingGenericParameterException(type);
            }

            if (type.IsArray)
            {
                return TypeToken.CreateArray(BuildTypeToken(GetRequiredElementType(type), inProgress));
            }

            if (type.IsPointer)
            {
                return TypeToken.CreatePointer(BuildTypeToken(GetRequiredElementType(type), inProgress));
            }

            if (type.IsByRef)
            {
                return TypeToken.CreateByRef(BuildTypeToken(GetRequiredElementType(type), inProgress));
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                Type genericDefinition;
                Type[] genericArguments;
                try
                {
                    genericDefinition = type.GetGenericTypeDefinition();
                    genericArguments = type.GetGenericArguments();
                }
                catch (NotSupportedException)
                {
                    // TypeBuilderInstantiation doesn't support GetGenericTypeDefinition.
                    // Parse the FullName to extract the generic definition and arguments.
                    var instName = type.FullName ?? type.Name;
                    var instBacktick = instName.IndexOf('`');
                    if (instBacktick > 0)
                    {
                        var bracketStart = instName.IndexOf('[', instBacktick);
                        if (bracketStart > 0)
                        {
                            var defName = instName.Substring(0, bracketStart);
                            var defToken = TypeToken.CreatePackageTypeRef(GetPackageImportPath(type), defName);
                            var argsStr = instName.Substring(bracketStart);
                            var argTokens = ParseGenericArgumentsFromName(argsStr, inProgress);
                            return TypeToken.CreateGenericInst(defToken, argTokens);
                        }
                    }
                    return TypeToken.CreatePackageTypeRef(type.Namespace ?? "", type.Name);
                }
                var argumentTokens = genericArguments.Select(arg => BuildTypeToken(arg, inProgress)).ToArray();
                return TypeToken.CreateGenericInst(BuildTypeToken(genericDefinition, inProgress), argumentTokens);
            }

            // Last-resort check for constructed generic types that bypassed the IsGenericType check
            // (e.g., TypeBuilderInstantiation or types with NgoProxyType arguments where
            // reflection doesn't report IsGenericType correctly)
            var typeName = type.FullName ?? type.Name;
            var backtickIndex = typeName.IndexOf('`');
            if (backtickIndex > 0 && typeName.IndexOf('[', backtickIndex) > 0
                && !typeName.EndsWith("[]") && !type.IsGenericTypeDefinition)
            {
                try
                {
                    var genericDefinition = type.GetGenericTypeDefinition();
                    var genericArguments = type.GetGenericArguments();
                    var argumentTokens = new TypeToken[genericArguments.Length];
                    for (int i = 0; i < genericArguments.Length; i++)
                    {
                        argumentTokens[i] = BuildTypeToken(genericArguments[i], inProgress);
                    }
                    return TypeToken.CreateGenericInst(BuildTypeToken(genericDefinition, inProgress), argumentTokens);
                }
                catch (NotSupportedException)
                {
                    // Constructed generic with proxy type args — parse the name to extract parts.
                    // Format: "Namespace.Type`N[[ArgFullName, Assembly], ...]"
                    var defName = typeName.Substring(0, typeName.IndexOf('[', backtickIndex));
                    var defToken = TypeToken.CreatePackageTypeRef(GetPackageImportPath(type), defName);

                    // Parse type arguments from the FullName string
                    var argsStr = typeName.Substring(typeName.IndexOf('[', backtickIndex));
                    var argTokens = ParseGenericArgumentsFromName(argsStr, inProgress);
                    return TypeToken.CreateGenericInst(defToken, argTokens);
                }
            }

            // For generic definitions, strip the type arg brackets: "Slice`1[[T]]" → "Slice`1"
            if (backtickIndex > 0 && type.IsGenericTypeDefinition)
            {
                var bracketStart = typeName.IndexOf('[', backtickIndex);
                if (bracketStart > 0)
                {
                    typeName = typeName.Substring(0, bracketStart);
                }
            }

            var packagePath = GetPackageImportPath(type);
            return TypeToken.CreatePackageTypeRef(packagePath, typeName);
        }

        private TypeToken[] ParseGenericArgumentsFromName(string argsStr, HashSet<Type> inProgress)
        {
            // argsStr format: "[[FullName1, Assembly1],[FullName2, Assembly2]]"
            // or just "[FullName1,FullName2]" for simple names
            var result = new List<TypeToken>();
            int depth = 0;
            int start = -1;
            for (int i = 0; i < argsStr.Length; i++)
            {
                if (argsStr[i] == '[')
                {
                    depth++;
                    if (depth == 2)
                    {
                        start = i + 1;
                    }
                }
                else if (argsStr[i] == ']')
                {
                    depth--;
                    if (depth == 1 && start >= 0)
                    {
                        var argFullName = argsStr.Substring(start, i - start);
                        // Strip assembly qualification: "TypeName, Assembly, Version=..." → "TypeName"
                        var commaIndex = argFullName.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            argFullName = argFullName.Substring(0, commaIndex).Trim();
                        }
                        // Try to resolve as a known type
                        var argType = Type.GetType(argFullName) ?? RuntimeAssembly.GetType(argFullName);
                        if (argType != null)
                        {
                            result.Add(BuildTypeToken(argType, inProgress));
                        }
                        else
                        {
                            // Might be a TypeDef in this archive
                            result.Add(TypeToken.CreateTypeDef(argFullName));
                        }
                        start = -1;
                    }
                }
            }
            return result.ToArray();
        }

        private static readonly Assembly RuntimeAssembly = typeof(Ngo.Runtime.Slice<>).Assembly;

        private MethodToken BuildMethodToken(MethodInfo method)
        {
            if (method == null)
            {
                throw new InvalidOperationException("NgoWriter: attempted to serialize a null method reference");
            }

            if (method.IsGenericMethod && !method.IsGenericMethodDefinition)
            {
                var genericDefinition = method.GetGenericMethodDefinition();
                var typeArguments = method.GetGenericArguments();
                var typeArgumentTokens = typeArguments.Select(BuildTypeToken).ToArray();
                var baseMethodToken = BuildMethodToken(genericDefinition);
                return MethodToken.CreateMethodSpec(baseMethodToken, typeArgumentTokens);
            }

            var declaringType = method.DeclaringType;
            var parameterTokens = BuildParameterTypeTokens(method.GetParameters(), declaringType);
            var returnTypeToken = BuildTypeToken(method.ReturnType);

            if (declaringType is NgoProxyType)
            {
                return MethodToken.CreateMethodDef(BuildTypeToken(declaringType), method.Name, parameterTokens, returnTypeToken);
            }

            return MethodToken.CreateMemberRef(BuildTypeToken(declaringType!), method.Name, parameterTokens, returnTypeToken);
        }

        private MethodToken BuildConstructorToken(ConstructorInfo constructor)
        {
            if (constructor == null)
            {
                throw new InvalidOperationException("NgoWriter: attempted to serialize a null constructor reference");
            }

            var declaringType = constructor.DeclaringType!;
            var parameterTokens = BuildParameterTypeTokens(constructor.GetParameters(), declaringType);
            var returnTypeToken = TypeToken.CreatePrimitive(PrimitiveTypeKind.Void);

            if (declaringType is NgoProxyType)
            {
                return MethodToken.CreateMethodDef(BuildTypeToken(declaringType), ".ctor", parameterTokens, returnTypeToken);
            }

            return MethodToken.CreateMemberRef(BuildTypeToken(declaringType), ".ctor", parameterTokens, returnTypeToken);
        }

        private FieldToken BuildFieldToken(FieldInfo field)
        {
            var declaringType = field.DeclaringType!;

            if (declaringType is NgoProxyType)
            {
                return FieldToken.CreateFieldDef(BuildTypeToken(declaringType), field.Name);
            }

            return FieldToken.CreateMemberRef(BuildTypeToken(declaringType), field.Name);
        }

        private TypeToken[] BuildParameterTypeTokens(ParameterInfo[] parameters, Type? declaringType)
        {
            Type[]? declaringTypeArguments = null;
            Type[]? declaringTypeDefParameters = null;
            if (declaringType != null && declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition)
            {
                try
                {
                    declaringTypeArguments = declaringType.GetGenericArguments();
                    declaringTypeDefParameters = declaringType.GetGenericTypeDefinition().GetGenericArguments();
                }
                catch (NotSupportedException)
                {
                    // TypeBuilderInstantiation may not support GetGenericTypeDefinition.
                    // Try to extract type args directly from the type name or fall through.
                }
            }

            var parameterTokens = new TypeToken[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                var parameterType = parameters[index].ParameterType;
                parameterType = SubstituteGenericParameters(parameterType, declaringType,
                    declaringTypeArguments, declaringTypeDefParameters);
                parameterTokens[index] = BuildTypeToken(parameterType);
            }
            return parameterTokens;
        }

        private static Type SubstituteGenericParameters(Type type, Type? declaringType,
            Type[]? declaringTypeArguments, Type[]? declaringTypeDefParameters)
        {
            if (type.IsGenericParameter)
            {
                if (declaringTypeArguments != null && declaringTypeDefParameters != null)
                {
                    for (int genericIndex = 0; genericIndex < declaringTypeDefParameters.Length; genericIndex++)
                    {
                        if (declaringTypeDefParameters[genericIndex] == type
                            || declaringTypeDefParameters[genericIndex].Name == type.Name)
                        {
                            return declaringTypeArguments[genericIndex];
                        }
                    }
                }

                if (declaringType != null && declaringType.IsGenericType)
                {
                    var actualArgs = declaringType.GenericTypeArguments;
                    if (type.GenericParameterPosition >= 0 && type.GenericParameterPosition < actualArgs.Length)
                    {
                        return actualArgs[type.GenericParameterPosition];
                    }
                }

                return type;
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var substituted = SubstituteGenericParameters(elementType, declaringType,
                    declaringTypeArguments, declaringTypeDefParameters);
                if (substituted != elementType)
                {
                    return substituted.MakeArrayType();
                }
            }

            if (type.IsByRef)
            {
                var elementType = type.GetElementType()!;
                var substituted = SubstituteGenericParameters(elementType, declaringType,
                    declaringTypeArguments, declaringTypeDefParameters);
                if (substituted != elementType)
                {
                    return substituted.MakeByRefType();
                }
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var genericArgs = type.GetGenericArguments();
                bool changed = false;
                var newArgs = new Type[genericArgs.Length];
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    newArgs[i] = SubstituteGenericParameters(genericArgs[i], declaringType,
                        declaringTypeArguments, declaringTypeDefParameters);
                    if (newArgs[i] != genericArgs[i])
                    {
                        changed = true;
                    }
                }
                if (changed)
                {
                    return type.GetGenericTypeDefinition().MakeGenericType(newArgs);
                }
            }

            return type;
        }

        private static string GetPackageImportPath(Type type)
        {
            // TypeBuilder-based types don't support GetCustomAttribute
            if (type is TypeBuilder || type is TypeDelegator
                || type.GetType().Name == "TypeBuilderInstantiation")
            {
                return type.Namespace ?? "";
            }

            try
            {
                var goTypeAttribute = type.GetCustomAttribute<GoTypeAttribute>();
                if (goTypeAttribute?.Package != null)
                {
                    return goTypeAttribute.Package;
                }

                var declaringType = type.DeclaringType ?? type;
                var goPackageAttribute = declaringType.GetCustomAttribute<GoPackageAttribute>();
                if (goPackageAttribute != null)
                {
                    return goPackageAttribute.ImportPath;
                }
            }
            catch (NotSupportedException)
            {
                // Some reflection types don't support custom attributes
            }

            return type.Namespace ?? "";
        }

        // ----- Static type name helpers (used by other serializers for Section 2 metadata) -----

        internal static string GetTypeNameStatic(Type type) => GetTypeName(type);

        private static string GetTypeName(Type type)
        {
            return GetTypeName(type, new HashSet<Type>(ReferenceEqualityComparer.Instance));
        }

        private static string GetTypeName(Type type, HashSet<Type> inProgress)
        {
            if (!inProgress.Add(type))
            {
                return type.Name ?? "$$circular";
            }

            try
            {
                return GetTypeNameCore(type, inProgress);
            }
            finally
            {
                inProgress.Remove(type);
            }
        }

        private static string GetTypeNameCore(Type type, HashSet<Type> inProgress)
        {
            if (type == typeof(void)) { return "System.Void"; }
            if (type == typeof(object)) { return "System.Object"; }
            if (type == typeof(GoString)) { return "Ngo.Runtime.GoString"; }
            if (type == typeof(int)) { return "System.Int32"; }
            if (type == typeof(long)) { return "System.Int64"; }
            if (type == typeof(bool)) { return "System.Boolean"; }
            if (type == typeof(byte)) { return "System.Byte"; }
            if (type == typeof(short)) { return "System.Int16"; }
            if (type == typeof(float)) { return "System.Single"; }
            if (type == typeof(double)) { return "System.Double"; }
            if (type == typeof(char)) { return "System.Char"; }
            if (type == typeof(uint)) { return "System.UInt32"; }
            if (type == typeof(ulong)) { return "System.UInt64"; }
            if (type == typeof(ushort)) { return "System.UInt16"; }
            if (type == typeof(sbyte)) { return "System.SByte"; }
            if (type == typeof(nint)) { return "System.IntPtr"; }
            if (type == typeof(nuint)) { return "System.UIntPtr"; }

            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (type.IsArray)
            {
                return GetTypeName(type.GetElementType()!, inProgress) + "[]";
            }

            if (type.IsByRef)
            {
                return GetTypeName(type.GetElementType()!, inProgress) + "&";
            }

            if (type.IsPointer)
            {
                return GetTypeName(type.GetElementType()!, inProgress) + "*";
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var genericDefinition = type.GetGenericTypeDefinition();
                var arguments = type.GetGenericArguments();
                var argumentNames = string.Join(",", Array.ConvertAll(arguments, arg => GetTypeName(arg, inProgress)));
                return GetTypeName(genericDefinition, inProgress) + "[" + argumentNames + "]";
            }

            var result = type.FullName ?? type.Name;
            // .NET TypeBuilder.FullName escapes special characters with backslash
            // (e.g., backtick: \`, brackets: \[\], plus: \+). Strip all escapes
            // since the archive format doesn't use .NET type name escaping.
            if (result.IndexOf('\\') >= 0)
            {
                var cleaned = new System.Text.StringBuilder(result.Length);
                for (int charIndex = 0; charIndex < result.Length; charIndex++)
                {
                    if (result[charIndex] == '\\' && charIndex + 1 < result.Length)
                    {
                        charIndex++;
                        cleaned.Append(result[charIndex]);
                    }
                    else
                    {
                        cleaned.Append(result[charIndex]);
                    }
                }
                result = cleaned.ToString();
            }
            return result;
        }

        // ----- Private helpers -----

        private static ILGenerator CreateFactory()
        {
            var dynamicMethod = new DynamicMethod("ngo_factory_" + Guid.NewGuid().ToString("N"), typeof(void), Type.EmptyTypes);
            return dynamicMethod.GetILGenerator();
        }

        private static int GetLabelId(Label label)
        {
            return label.GetHashCode();
        }

        private void TrackStack(OpCode op, int extraPush = 0, int extraPop = 0)
        {
            // StackBehaviourPush tells us how many values the opcode pushes.
            int push = op.StackBehaviourPush switch
            {
                StackBehaviour.Push0 => 0,
                StackBehaviour.Push1 => 1,
                StackBehaviour.Push1_push1 => 2,
                StackBehaviour.Pushi => 1,
                StackBehaviour.Pushi8 => 1,
                StackBehaviour.Pushr4 => 1,
                StackBehaviour.Pushr8 => 1,
                StackBehaviour.Pushref => 1,
                StackBehaviour.Varpush => extraPush,   // call/callvirt: caller supplies via extraPush
                _ => 0,
            };

            // StackBehaviourPop tells us how many values the opcode pops.
            int pop = op.StackBehaviourPop switch
            {
                StackBehaviour.Pop0 => 0,
                StackBehaviour.Pop1 => 1,
                StackBehaviour.Pop1_pop1 => 2,
                StackBehaviour.Popi => 1,
                StackBehaviour.Popi_pop1 => 2,
                StackBehaviour.Popi_popi => 2,
                StackBehaviour.Popi_popi8 => 2,
                StackBehaviour.Popi_popi_popi => 3,
                StackBehaviour.Popi_popr4 => 2,
                StackBehaviour.Popi_popr8 => 2,
                StackBehaviour.Popref => 1,
                StackBehaviour.Popref_pop1 => 2,
                StackBehaviour.Popref_popi => 2,
                StackBehaviour.Popref_popi_pop1 => 3,
                StackBehaviour.Popref_popi_popi => 3,
                StackBehaviour.Popref_popi_popi8 => 3,
                StackBehaviour.Popref_popi_popr4 => 3,
                StackBehaviour.Popref_popi_popr8 => 3,
                StackBehaviour.Popref_popi_popref => 3,
                StackBehaviour.Varpop => extraPop,     // call/callvirt/ret: caller supplies via extraPop
                _ => 0,
            };

            _currentStackDepth += push - pop;
            if (_currentStackDepth < 0) _currentStackDepth = 0; // guard against mis-tracking in dead code
            if (_currentStackDepth > _maxStackDepth) _maxStackDepth = _currentStackDepth;
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
            for (int byteIndex = 0; byteIndex < 8; byteIndex++)
            {
                _code.WriteByte((byte)((value >> (byteIndex * 8)) & 0xFF));
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
            // Use GetBuffer() for in-place patching — it returns the live backing array so
            // writes are reflected in the MemoryStream. All fixup offsets are within [0, Length)
            // because they were recorded as IL was emitted; the extra capacity beyond Length is
            // never touched. This is safe as long as callers always use ToArray() (not GetBuffer())
            // to obtain the final bytes, which GetILBytes() does.
            var buffer = _code.GetBuffer();
            foreach (var fixup in _branchFixups)
            {
                if (!_labelOffsets.TryGetValue(fixup.LabelId, out var targetOffset))
                {
                    throw new InvalidOperationException(
                        $"NgoWriter: label {fixup.LabelId} referenced at IL offset {fixup.Offset} was never marked");
                }

                if (fixup.IsShort)
                {
                    var relativeOffset = targetOffset - (fixup.Offset + 1);
                    buffer[fixup.Offset] = (byte)(sbyte)relativeOffset;
                }
                else
                {
                    var baseOff = fixup.BaseOffset > 0 ? fixup.BaseOffset : fixup.Offset + 4;
                    var relativeOffset = targetOffset - baseOff;
                    buffer[fixup.Offset] = (byte)(relativeOffset & 0xFF);
                    buffer[fixup.Offset + 1] = (byte)((relativeOffset >> 8) & 0xFF);
                    buffer[fixup.Offset + 2] = (byte)((relativeOffset >> 16) & 0xFF);
                    buffer[fixup.Offset + 3] = (byte)((relativeOffset >> 24) & 0xFF);
                }
            }
        }

        private static bool IsShortBranch(OpCode op)
        {
            return op.OperandType == OperandType.ShortInlineBrTarget;
        }

        // ----- Data types -----

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
            public int BaseOffset;
        }
    }
}
