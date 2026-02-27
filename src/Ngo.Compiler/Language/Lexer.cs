// -----------------------------------------------------------------------
// <copyright file="Lexer.cs" company="Ziad">
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

namespace Ngo.Compiler.Language
{
    public sealed class Lexer
    {
        private readonly string _source;
        private int _pos;
        private readonly List<SyntaxExtra> _triviaBuilder = new();

        // For auto-semicolon insertion
        private SyntaxKind _lastNonExtraKind;
        private bool _pendingSemicolon;

        public Lexer(string source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _pos = 0;
        }

        public IReadOnlyList<SyntaxToken> LexAll()
        {
            var tokens = new List<SyntaxToken>();
            while (true)
            {
                var token = NextToken();
                tokens.Add(token);
                if (token.Kind == SyntaxKind.EndOfFileToken)
                    break;
            }
            return tokens;
        }

        public SyntaxToken NextToken()
        {
            // If a previous token's trailing trivia contained a newline that triggers
            // auto-semicolon insertion, emit that semicolon now.
            if (_pendingSemicolon)
            {
                _pendingSemicolon = false;
                _lastNonExtraKind = SyntaxKind.SemicolonToken;
                return new SyntaxToken(SyntaxKind.SemicolonToken, "", _pos);
            }

            // Collect leading trivia (whitespace, comments, newlines that don't trigger semicolons)
            var leadingExtra = ScanLeadingExtra();

            // Check for end of file
            if (_pos >= _source.Length)
            {
                // May need auto-semicolon before EOF
                if (NeedsAutoSemicolon(_lastNonExtraKind))
                {
                    _lastNonExtraKind = SyntaxKind.SemicolonToken;
                    return new SyntaxToken(
                        SyntaxKind.SemicolonToken, "", _pos,
                        leadingExtra: leadingExtra);
                }

                return new SyntaxToken(
                    SyntaxKind.EndOfFileToken, "", _pos,
                    leadingExtra: leadingExtra);
            }

            // Scan the actual token
            int tokenStart = _pos;
            SyntaxKind kind = ScanToken(out object? value);
            string text = _source.Substring(tokenStart, _pos - tokenStart);

            // Collect trailing trivia (whitespace and comments on the same line)
            var trailingExtra = ScanTrailingExtra(kind);

            var token = new SyntaxToken(kind, text, tokenStart, value, leadingExtra, trailingExtra);
            _lastNonExtraKind = kind;
            return token;
        }

        private IReadOnlyList<SyntaxExtra> ScanLeadingExtra()
        {
            _triviaBuilder.Clear();

            while (_pos < _source.Length)
            {
                char c = _source[_pos];

                if (c == ' ' || c == '\t' || c == '\v' || c == '\r')
                {
                    ScanWhitespaceExtra();
                }
                else if (c == '\n')
                {
                    _triviaBuilder.Add(new SyntaxExtra(SyntaxKind.EndOfLineExtra, "\n", _pos));
                    _pos++;
                }
                else if (c == '/' && _pos + 1 < _source.Length)
                {
                    char next = _source[_pos + 1];
                    if (next == '/')
                    {
                        ScanLineCommentExtra();
                    }
                    else if (next == '*')
                    {
                        ScanBlockCommentExtra();
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            return _triviaBuilder.Count > 0 ? _triviaBuilder.ToArray() : SyntaxToken.EmptyExtra;
        }

        private IReadOnlyList<SyntaxExtra> ScanTrailingExtra(SyntaxKind tokenKind)
        {
            _triviaBuilder.Clear();

            while (_pos < _source.Length)
            {
                char c = _source[_pos];

                if (c == ' ' || c == '\t' || c == '\v' || c == '\r')
                {
                    ScanWhitespaceExtra();
                }
                else if (c == '/' && _pos + 1 < _source.Length)
                {
                    char next = _source[_pos + 1];
                    if (next == '/')
                    {
                        ScanLineCommentExtra();
                    }
                    else if (next == '*')
                    {
                        ScanBlockCommentExtra();
                    }
                    else
                    {
                        break;
                    }
                }
                else if (c == '\n')
                {
                    // Include the newline in trailing trivia, then stop.
                    // If the token we just scanned triggers auto-semicolon,
                    // set the pending flag so NextToken emits it.
                    if (NeedsAutoSemicolon(tokenKind))
                        _pendingSemicolon = true;

                    _triviaBuilder.Add(new SyntaxExtra(SyntaxKind.EndOfLineExtra, "\n", _pos));
                    _pos++;
                    break;
                }
                else
                {
                    break;
                }
            }

            return _triviaBuilder.Count > 0 ? _triviaBuilder.ToArray() : SyntaxToken.EmptyExtra;
        }

        private void ScanWhitespaceExtra()
        {
            int start = _pos;
            while (_pos < _source.Length)
            {
                char c = _source[_pos];
                if (c == ' ' || c == '\t' || c == '\v' || c == '\r')
                    _pos++;
                else
                    break;
            }

            _triviaBuilder.Add(new SyntaxExtra(
                SyntaxKind.WhitespaceExtra,
                _source.Substring(start, _pos - start),
                start));
        }

        private void ScanLineCommentExtra()
        {
            int start = _pos;
            // Skip past //
            _pos += 2;
            while (_pos < _source.Length && _source[_pos] != '\n')
                _pos++;

            _triviaBuilder.Add(new SyntaxExtra(
                SyntaxKind.LineCommentExtra,
                _source.Substring(start, _pos - start),
                start));
        }

        private void ScanBlockCommentExtra()
        {
            int start = _pos;
            // Skip past /*
            _pos += 2;
            while (_pos + 1 < _source.Length)
            {
                if (_source[_pos] == '*' && _source[_pos + 1] == '/')
                {
                    _pos += 2;
                    break;
                }
                _pos++;
            }

            // Handle unterminated block comment
            if (_pos >= _source.Length && !(_source.Length >= 4 && _source[_source.Length - 2] == '*' && _source[_source.Length - 1] == '/'))
                _pos = _source.Length;

            _triviaBuilder.Add(new SyntaxExtra(
                SyntaxKind.BlockCommentExtra,
                _source.Substring(start, _pos - start),
                start));
        }

        private SyntaxKind ScanToken(out object? value)
        {
            value = null;
            char c = _source[_pos];

            switch (c)
            {
                case '+':
                    _pos++;
                    if (_pos < _source.Length)
                    {
                        if (_source[_pos] == '+') { _pos++; return SyntaxKind.PlusPlusToken; }
                        if (_source[_pos] == '=') { _pos++; return SyntaxKind.PlusEqualsToken; }
                    }
                    return SyntaxKind.PlusToken;

                case '-':
                    _pos++;
                    if (_pos < _source.Length)
                    {
                        if (_source[_pos] == '-') { _pos++; return SyntaxKind.MinusMinusToken; }
                        if (_source[_pos] == '=') { _pos++; return SyntaxKind.MinusEqualsToken; }
                    }
                    return SyntaxKind.MinusToken;

                case '*':
                    _pos++;
                    if (_pos < _source.Length && _source[_pos] == '=') { _pos++; return SyntaxKind.StarEqualsToken; }
                    return SyntaxKind.StarToken;

                case '/':
                    _pos++;
                    if (_pos < _source.Length && _source[_pos] == '=') { _pos++; return SyntaxKind.SlashEqualsToken; }
                    return SyntaxKind.SlashToken;

                case '%':
                    _pos++;
                    if (_pos < _source.Length && _source[_pos] == '=') { _pos++; return SyntaxKind.PercentEqualsToken; }
                    return SyntaxKind.PercentToken;

                case '&':
                    _pos++;
                    if (_pos < _source.Length)
                    {
                        if (_source[_pos] == '&') { _pos++; return SyntaxKind.AmpersandAmpersandToken; }
                        if (_source[_pos] == '=') { _pos++; return SyntaxKind.AmpersandEqualsToken; }
                        if (_source[_pos] == '^')
                        {
                            _pos++;
                            if (_pos < _source.Length && _source[_pos] == '=') { _pos++; return SyntaxKind.AmpersandCaretEqualsToken; }
                            return SyntaxKind.AmpersandCaretToken;
                        }
                    }
                    return SyntaxKind.AmpersandToken;

                case '|':
                    _pos++;
                    if (_pos < _source.Length)
                    {
                        if (_source[_pos] == '|') { _pos++; return SyntaxKind.PipePipeToken; }
                        if (_source[_pos] == '=') { _pos++; return SyntaxKind.PipeEqualsToken; }
                    }
                    return SyntaxKind.PipeToken;

                case '^':
                    _pos++;
                    if (_pos < _source.Length && _source[_pos] == '=') { _pos++; return SyntaxKind.CaretEqualsToken; }
                    return SyntaxKind.CaretToken;

                case '<':
                    _pos++;
                    if (_pos < _source.Length)
                    {
                        if (_source[_pos] == '-') { _pos++; return SyntaxKind.LessThanMinusToken; }
                        if (_source[_pos] == '<')
                        {
                            _pos++;
                            if (_pos < _source.Length && _source[_pos] == '=') { _pos++; return SyntaxKind.LessThanLessThanEqualsToken; }
                            return SyntaxKind.LessThanLessThanToken;
                        }
                        if (_source[_pos] == '=') { _pos++; return SyntaxKind.LessThanEqualsToken; }
                    }
                    return SyntaxKind.LessThanToken;

                case '>':
                    _pos++;
                    if (_pos < _source.Length)
                    {
                        if (_source[_pos] == '>')
                        {
                            _pos++;
                            if (_pos < _source.Length && _source[_pos] == '=') { _pos++; return SyntaxKind.GreaterThanGreaterThanEqualsToken; }
                            return SyntaxKind.GreaterThanGreaterThanToken;
                        }
                        if (_source[_pos] == '=') { _pos++; return SyntaxKind.GreaterThanEqualsToken; }
                    }
                    return SyntaxKind.GreaterThanToken;

                case '=':
                    _pos++;
                    if (_pos < _source.Length && _source[_pos] == '=') { _pos++; return SyntaxKind.EqualsEqualsToken; }
                    return SyntaxKind.EqualsToken;

                case '!':
                    _pos++;
                    if (_pos < _source.Length && _source[_pos] == '=') { _pos++; return SyntaxKind.ExclamationEqualsToken; }
                    return SyntaxKind.ExclamationToken;

                case ':':
                    _pos++;
                    if (_pos < _source.Length && _source[_pos] == '=') { _pos++; return SyntaxKind.ColonEqualsToken; }
                    return SyntaxKind.ColonToken;

                case '.':
                    if (_pos + 2 < _source.Length && _source[_pos + 1] == '.' && _source[_pos + 2] == '.')
                    {
                        _pos += 3;
                        return SyntaxKind.EllipsisToken;
                    }
                    if (_pos + 1 < _source.Length && IsDigit(_source[_pos + 1]))
                    {
                        return ScanNumber(out value);
                    }
                    _pos++;
                    return SyntaxKind.DotToken;

                case '(':  _pos++; return SyntaxKind.OpenParenToken;
                case ')':  _pos++; return SyntaxKind.CloseParenToken;
                case '{':  _pos++; return SyntaxKind.OpenBraceToken;
                case '}':  _pos++; return SyntaxKind.CloseBraceToken;
                case '[':  _pos++; return SyntaxKind.OpenBracketToken;
                case ']':  _pos++; return SyntaxKind.CloseBracketToken;
                case ',':  _pos++; return SyntaxKind.CommaToken;
                case ';':  _pos++; return SyntaxKind.SemicolonToken;

                case '\'': return ScanRuneLiteral(out value);
                case '"':  return ScanStringLiteral(out value);
                case '`':  return ScanRawStringLiteral(out value);

                default:
                    if (IsDigit(c))
                        return ScanNumber(out value);

                    if (IsIdentStart(c))
                        return ScanIdentifierOrKeyword(out value);

                    _pos++;
                    return SyntaxKind.ErrorToken;
            }
        }

        private SyntaxKind ScanIdentifierOrKeyword(out object? value)
        {
            int start = _pos;
            _pos++;
            while (_pos < _source.Length && IsIdentPart(_source[_pos]))
                _pos++;

            string text = _source.Substring(start, _pos - start);
            value = null;

            return text switch
            {
                "break"       => SyntaxKind.BreakKeyword,
                "case"        => SyntaxKind.CaseKeyword,
                "chan"         => SyntaxKind.ChanKeyword,
                "const"       => SyntaxKind.ConstKeyword,
                "continue"    => SyntaxKind.ContinueKeyword,
                "default"     => SyntaxKind.DefaultKeyword,
                "defer"       => SyntaxKind.DeferKeyword,
                "else"        => SyntaxKind.ElseKeyword,
                "fallthrough" => SyntaxKind.FallthroughKeyword,
                "for"         => SyntaxKind.ForKeyword,
                "func"        => SyntaxKind.FuncKeyword,
                "go"          => SyntaxKind.GoKeyword,
                "goto"        => SyntaxKind.GotoKeyword,
                "if"          => SyntaxKind.IfKeyword,
                "import"      => SyntaxKind.ImportKeyword,
                "interface"   => SyntaxKind.InterfaceKeyword,
                "map"         => SyntaxKind.MapKeyword,
                "package"     => SyntaxKind.PackageKeyword,
                "range"       => SyntaxKind.RangeKeyword,
                "return"      => SyntaxKind.ReturnKeyword,
                "select"      => SyntaxKind.SelectKeyword,
                "struct"      => SyntaxKind.StructKeyword,
                "switch"      => SyntaxKind.SwitchKeyword,
                "type"        => SyntaxKind.TypeKeyword,
                "var"         => SyntaxKind.VarKeyword,
                _             => SyntaxKind.IdentifierToken,
            };
        }

        private SyntaxKind ScanNumber(out object? value)
        {
            int start = _pos;
            value = null;

            // Leading dot case (.123)
            if (_source[_pos] == '.')
            {
                _pos++;
                ScanDigits();
                ScanExponent();
                ScanImaginary(ref value);
                return value != null ? SyntaxKind.ImaginaryLiteralToken : SyntaxKind.FloatLiteralToken;
            }

            // 0x, 0X — hex
            if (_source[_pos] == '0' && _pos + 1 < _source.Length)
            {
                char next = _source[_pos + 1];
                if (next == 'x' || next == 'X')
                {
                    _pos += 2;
                    ScanHexDigits();

                    bool isFloat = false;

                    // Optional fractional part: 0x1.fp10
                    if (_pos < _source.Length && _source[_pos] == '.')
                    {
                        _pos++;
                        ScanHexDigits();
                        isFloat = true;
                    }

                    // Hex float exponent (p/P) — mandatory for hex floats
                    if (_pos < _source.Length && (_source[_pos] == 'p' || _source[_pos] == 'P'))
                    {
                        _pos++;
                        if (_pos < _source.Length && (_source[_pos] == '+' || _source[_pos] == '-'))
                            _pos++;
                        ScanDigits();
                        isFloat = true;
                    }

                    // Imaginary suffix
                    if (_pos < _source.Length && _source[_pos] == 'i')
                    {
                        _pos++;
                        return SyntaxKind.ImaginaryLiteralToken;
                    }

                    return isFloat ? SyntaxKind.FloatLiteralToken : SyntaxKind.IntLiteralToken;
                }

                if (next == 'b' || next == 'B')
                {
                    _pos += 2;
                    ScanBinaryDigits();
                    return SyntaxKind.IntLiteralToken;
                }

                if (next == 'o' || next == 'O')
                {
                    _pos += 2;
                    ScanOctalDigits();
                    return SyntaxKind.IntLiteralToken;
                }

                // Legacy octal: 0777
                if (IsOctalDigit(next))
                {
                    _pos++;
                    ScanOctalDigits();
                    // Could still be a float if followed by 8/9 or '.'
                    if (_pos < _source.Length && (_source[_pos] == '.' || _source[_pos] == '8' || _source[_pos] == '9'))
                    {
                        // Fall through to decimal handling
                        ScanDigits();
                    }
                    else
                    {
                        return SyntaxKind.IntLiteralToken;
                    }
                }
            }

            // Decimal integer or float
            ScanDigits();

            if (_pos < _source.Length && _source[_pos] == '.')
            {
                // Check it's not an ellipsis
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '.')
                {
                    // Not a float — stop here
                    return SyntaxKind.IntLiteralToken;
                }

                _pos++;
                ScanDigits();
                ScanExponent();
                ScanImaginary(ref value);
                return value != null ? SyntaxKind.ImaginaryLiteralToken : SyntaxKind.FloatLiteralToken;
            }

            if (ScanExponent())
            {
                ScanImaginary(ref value);
                return value != null ? SyntaxKind.ImaginaryLiteralToken : SyntaxKind.FloatLiteralToken;
            }

            if (_pos < _source.Length && _source[_pos] == 'i')
            {
                _pos++;
                return SyntaxKind.ImaginaryLiteralToken;
            }

            return SyntaxKind.IntLiteralToken;
        }

        private void ScanDigits()
        {
            while (_pos < _source.Length && (IsDigit(_source[_pos]) || _source[_pos] == '_'))
                _pos++;
        }

        private void ScanHexDigits()
        {
            while (_pos < _source.Length && (IsHexDigit(_source[_pos]) || _source[_pos] == '_'))
                _pos++;
        }

        private void ScanOctalDigits()
        {
            while (_pos < _source.Length && (IsOctalDigit(_source[_pos]) || _source[_pos] == '_'))
                _pos++;
        }

        private void ScanBinaryDigits()
        {
            while (_pos < _source.Length && (_source[_pos] == '0' || _source[_pos] == '1' || _source[_pos] == '_'))
                _pos++;
        }

        private bool ScanExponent()
        {
            if (_pos < _source.Length && (_source[_pos] == 'e' || _source[_pos] == 'E'))
            {
                _pos++;
                if (_pos < _source.Length && (_source[_pos] == '+' || _source[_pos] == '-'))
                    _pos++;
                ScanDigits();
                return true;
            }
            return false;
        }

        private void ScanImaginary(ref object? value)
        {
            if (_pos < _source.Length && _source[_pos] == 'i')
            {
                _pos++;
                value = "imaginary"; // placeholder — real value parsing comes later
            }
        }

        private SyntaxKind ScanStringLiteral(out object? value)
        {
            int start = _pos;
            _pos++; // skip opening "
            value = null;

            while (_pos < _source.Length)
            {
                char c = _source[_pos];
                if (c == '\\')
                {
                    _pos++; // skip backslash
                    SkipEscapeChars();
                    continue;
                }
                if (c == '"')
                {
                    _pos++;
                    return SyntaxKind.StringLiteralToken;
                }
                if (c == '\n')
                {
                    // Unterminated string
                    return SyntaxKind.ErrorToken;
                }
                _pos++;
            }

            return SyntaxKind.ErrorToken; // unterminated
        }

        private SyntaxKind ScanRawStringLiteral(out object? value)
        {
            _pos++; // skip opening `
            value = null;

            while (_pos < _source.Length)
            {
                if (_source[_pos] == '`')
                {
                    _pos++;
                    return SyntaxKind.RawStringLiteralToken;
                }
                _pos++;
            }

            return SyntaxKind.ErrorToken; // unterminated
        }

        private SyntaxKind ScanRuneLiteral(out object? value)
        {
            _pos++; // skip opening '
            value = null;

            if (_pos >= _source.Length)
                return SyntaxKind.ErrorToken;

            if (_source[_pos] == '\\')
            {
                _pos++; // skip backslash
                SkipEscapeChars();
            }
            else
            {
                _pos++; // skip the rune character
            }

            if (_pos < _source.Length && _source[_pos] == '\'')
            {
                _pos++;
                return SyntaxKind.RuneLiteralToken;
            }

            return SyntaxKind.ErrorToken;
        }

        private void SkipEscapeChars()
        {
            if (_pos >= _source.Length) return;
            char esc = _source[_pos];
            switch (esc)
            {
                case 'x':
                    _pos += 3; // \x + 2 hex digits
                    break;
                case 'u':
                    _pos += 5; // \u + 4 hex digits
                    break;
                case 'U':
                    _pos += 9; // \U + 8 hex digits
                    break;
                case char d when d >= '0' && d <= '7':
                    _pos++; // first octal digit
                    if (_pos < _source.Length && _source[_pos] >= '0' && _source[_pos] <= '7') _pos++;
                    if (_pos < _source.Length && _source[_pos] >= '0' && _source[_pos] <= '7') _pos++;
                    break;
                default:
                    _pos++; // simple escape: \n, \t, \\, etc.
                    break;
            }
        }

        // Go spec: automatic semicolon insertion
        // A semicolon is automatically inserted after a line's final token if that token is:
        //   - an identifier
        //   - an integer, floating-point, imaginary, rune, or string literal
        //   - break, continue, fallthrough, or return
        //   - ++, --, ), ], or }
        private static bool NeedsAutoSemicolon(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.IdentifierToken:
                case SyntaxKind.IntLiteralToken:
                case SyntaxKind.FloatLiteralToken:
                case SyntaxKind.ImaginaryLiteralToken:
                case SyntaxKind.RuneLiteralToken:
                case SyntaxKind.StringLiteralToken:
                case SyntaxKind.RawStringLiteralToken:
                case SyntaxKind.BreakKeyword:
                case SyntaxKind.ContinueKeyword:
                case SyntaxKind.FallthroughKeyword:
                case SyntaxKind.ReturnKeyword:
                case SyntaxKind.PlusPlusToken:
                case SyntaxKind.MinusMinusToken:
                case SyntaxKind.CloseParenToken:
                case SyntaxKind.CloseBracketToken:
                case SyntaxKind.CloseBraceToken:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static bool IsOctalDigit(char c) => c >= '0' && c <= '7';

        private static bool IsHexDigit(char c)
        {
            if (c >= '0' && c <= '9') return true;
            c |= ' ';
            return c >= 'a' && c <= 'f';
        }

        private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';

        private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
