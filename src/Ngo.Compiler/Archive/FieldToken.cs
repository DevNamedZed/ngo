// -----------------------------------------------------------------------
// <copyright file="FieldToken.cs" company="Ziad">
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
    /// A structured field reference mirroring ECMA-335's FieldDef/MemberRef distinction.
    /// The declaring type is a full TypeToken, not a flat string.
    /// </summary>
    internal sealed class FieldToken
    {
        public FieldTokenKind Kind { get; }
        public TypeToken? DeclaringType { get; }
        public string FieldName { get; }

        private FieldToken(FieldTokenKind kind, TypeToken? declaringType = null,
            string fieldName = "")
        {
            Kind = kind;
            DeclaringType = declaringType;
            FieldName = fieldName;
        }

        public static FieldToken CreateFieldDef(TypeToken declaringType, string fieldName)
        {
            return new FieldToken(FieldTokenKind.FieldDef,
                declaringType: declaringType, fieldName: fieldName);
        }

        public static FieldToken CreateMemberRef(TypeToken declaringType, string fieldName)
        {
            return new FieldToken(FieldTokenKind.MemberRef,
                declaringType: declaringType, fieldName: fieldName);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)Kind);
            DeclaringType!.Write(writer);
            writer.Write(FieldName);
        }

        public static FieldToken Read(BinaryReader reader)
        {
            var kind = (FieldTokenKind)reader.ReadByte();
            var declaringType = TypeToken.Read(reader);
            var fieldName = reader.ReadString();
            return new FieldToken(kind, declaringType: declaringType, fieldName: fieldName);
        }
    }
}
