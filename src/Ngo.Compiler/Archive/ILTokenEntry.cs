// -----------------------------------------------------------------------
// <copyright file="ILTokenEntry.cs" company="Ziad">
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
using System.IO;

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// An entry in the IL token table: an offset in the IL byte stream paired with
    /// a structured token reference (type, method, field, or string).
    /// </summary>
    internal sealed class ILTokenEntry
    {
        public int Offset { get; }
        public ILTokenKind Kind { get; }
        public TypeToken? TypeToken { get; }
        public MethodToken? MethodToken { get; }
        public FieldToken? FieldToken { get; }
        public string? StringValue { get; }

        private ILTokenEntry(int offset, ILTokenKind kind, TypeToken? typeToken = null,
            MethodToken? methodToken = null, FieldToken? fieldToken = null, string? stringValue = null)
        {
            Offset = offset;
            Kind = kind;
            TypeToken = typeToken;
            MethodToken = methodToken;
            FieldToken = fieldToken;
            StringValue = stringValue;
        }

        public static ILTokenEntry CreateType(int offset, TypeToken typeToken)
        {
            return new ILTokenEntry(offset, ILTokenKind.Type, typeToken: typeToken);
        }

        public static ILTokenEntry CreateMethod(int offset, MethodToken methodToken)
        {
            return new ILTokenEntry(offset, ILTokenKind.Method, methodToken: methodToken);
        }

        public static ILTokenEntry CreateField(int offset, FieldToken fieldToken)
        {
            return new ILTokenEntry(offset, ILTokenKind.Field, fieldToken: fieldToken);
        }

        public static ILTokenEntry CreateString(int offset, string value)
        {
            return new ILTokenEntry(offset, ILTokenKind.String, stringValue: value);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Offset);
            writer.Write((byte)Kind);
            switch (Kind)
            {
                case ILTokenKind.Type:
                    TypeToken!.Write(writer);
                    break;
                case ILTokenKind.Method:
                    MethodToken!.Write(writer);
                    break;
                case ILTokenKind.Field:
                    FieldToken!.Write(writer);
                    break;
                case ILTokenKind.String:
                    writer.Write(StringValue!);
                    break;
            }
        }

        public static ILTokenEntry Read(BinaryReader reader)
        {
            var offset = reader.ReadInt32();
            var kind = (ILTokenKind)reader.ReadByte();
            switch (kind)
            {
                case ILTokenKind.Type:
                    return new ILTokenEntry(offset, kind, typeToken: TypeToken.Read(reader));
                case ILTokenKind.Method:
                    return new ILTokenEntry(offset, kind, methodToken: MethodToken.Read(reader));
                case ILTokenKind.Field:
                    return new ILTokenEntry(offset, kind, fieldToken: FieldToken.Read(reader));
                case ILTokenKind.String:
                    return new ILTokenEntry(offset, kind, stringValue: reader.ReadString());
                default:
                    throw new InvalidOperationException($"Unknown ILToken kind: {kind}");
            }
        }
    }

}
