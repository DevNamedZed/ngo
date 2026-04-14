// -----------------------------------------------------------------------
// <copyright file="CompileError.cs" company="Ziad">
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

using System.Collections.Generic;
using System.Linq;
using Ngo.Compiler.Language;

namespace Ngo.Compiler
{
    public enum ErrorSeverity
    {
        Error,
        Warning,
        Info
    }

    public enum ErrorCode
    {
        TokenExpected,
        SyntaxError,
        UndeclaredName,
        TypeMismatch,
        InvalidOperation,
        WrongArgumentCount,
        CannotAssign,
        AlreadyDeclared,
        MissingReturn,
        UnsupportedSyntax,
        InvalidConversion,
        InvalidBranch,
        UndefinedField,
        InvalidSelector,
        InvalidCompositeLiteral,
        InvalidAddressOf,
        InvalidIndex,
        InvalidSlice,
        InvalidRange,
        InvalidTypeAssert,
        InvalidMethodReceiver,
        WrongReturnCount,
        UnusedVariable,
        UnusedImport,
        UnreachableCode,
        UndefinedLabel,
        DuplicateLabel,
        GotoJumpsOverDeclaration,
        GotoJumpsIntoBlock,
        CannotInferTypeArguments,
        ConstraintNotSatisfied,
        WrongTypeArgumentCount,
        CgoCompilerNotFound,
        CgoDisabled,
        CgoProbeFailed,
    }

    public sealed class CompileError
    {
        public CompileError(ErrorSeverity severity, ErrorCode code, TextSpan location, string message)
        {
            Severity = severity;
            Code = code;
            Location = location;
            Message = message;
        }

        public ErrorSeverity Severity { get; }

        public ErrorCode Code { get; }

        public TextSpan Location { get; }

        public string Message { get; }

        public override string ToString() => $"{Severity} {Code}: {Message}";
    }

    public sealed class ErrorCollector
    {
        private readonly List<CompileError> _errors = new();

        public bool HasErrors => _errors.Any(e => e.Severity == ErrorSeverity.Error);
        public int Count => _errors.Count;
        public void TruncateTo(int count) { if (_errors.Count > count) _errors.RemoveRange(count, _errors.Count - count); }

        public IReadOnlyList<CompileError> ToReadOnlyList() => _errors;

        public void Report(ErrorSeverity severity, ErrorCode code, TextSpan location, string message)
        {
            _errors.Add(new CompileError(severity, code, location, message));
        }

        public void ReportError(TextSpan location, ErrorCode code, string message)
        {
            Report(ErrorSeverity.Error, code, location, message);
        }

        public void ReportWarning(TextSpan location, ErrorCode code, string message)
        {
            Report(ErrorSeverity.Warning, code, location, message);
        }

        public void AddRange(ErrorCollector other)
        {
            _errors.AddRange(other._errors);
        }
    }
}
