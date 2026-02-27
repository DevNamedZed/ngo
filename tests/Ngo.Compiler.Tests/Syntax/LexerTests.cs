// -----------------------------------------------------------------------
// <copyright file="LexerTests.cs" company="Ziad">
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Compiler.Tests.Syntax;

[TestClass]
public class LexerTests
{
    private static IReadOnlyList<SyntaxToken> Lex(string input)
    {
        var lexer = new Lexer(input);
        return lexer.LexAll();
    }

    private static IReadOnlyList<SyntaxToken> LexNonEof(string input)
    {
        return Lex(input)
            .Where(t => t.Kind != SyntaxKind.EndOfFileToken)
            .ToList();
    }

    /// <summary>
    /// Returns tokens excluding EOF and auto-inserted semicolons (empty text).
    /// Use for tests that don't care about auto-semicolons.
    /// </summary>
    private static IReadOnlyList<SyntaxToken> LexContent(string input)
    {
        return Lex(input)
            .Where(t => t.Kind != SyntaxKind.EndOfFileToken &&
                        !(t.Kind == SyntaxKind.SemicolonToken && t.Text.Length == 0))
            .ToList();
    }

    // ----------------------------------------------------------------
    // Basic tokens
    // ----------------------------------------------------------------

    [TestMethod]
    public void Empty_input_produces_only_eof()
    {
        var tokens = Lex("");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.EndOfFileToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Identifier()
    {
        var tokens = LexContent("main");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[0].Kind);
        Assert.AreEqual("main", tokens[0].Text);
    }

    [TestMethod]
    public void Integer_literal()
    {
        var tokens = LexContent("42");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.IntLiteralToken, tokens[0].Kind);
        Assert.AreEqual("42", tokens[0].Text);
    }

    [TestMethod]
    public void Hex_literal()
    {
        var tokens = LexContent("0xFF");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.IntLiteralToken, tokens[0].Kind);
        Assert.AreEqual("0xFF", tokens[0].Text);
    }

    [TestMethod]
    public void Binary_literal()
    {
        var tokens = LexContent("0b1010");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.IntLiteralToken, tokens[0].Kind);
        Assert.AreEqual("0b1010", tokens[0].Text);
    }

    [TestMethod]
    public void Octal_literal_new_syntax()
    {
        var tokens = LexContent("0o777");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.IntLiteralToken, tokens[0].Kind);
        Assert.AreEqual("0o777", tokens[0].Text);
    }

    [TestMethod]
    public void Digit_separators()
    {
        var tokens = LexContent("1_000_000");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.IntLiteralToken, tokens[0].Kind);
        Assert.AreEqual("1_000_000", tokens[0].Text);
    }

    [TestMethod]
    public void Float_literal()
    {
        var tokens = LexContent("3.14");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.FloatLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Float_with_exponent()
    {
        var tokens = LexContent("1e10");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.FloatLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Imaginary_literal()
    {
        var tokens = LexContent("2i");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.ImaginaryLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void String_literal()
    {
        var tokens = LexContent("\"hello\"");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.StringLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Raw_string_literal()
    {
        var tokens = LexContent("`raw string`");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.RawStringLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Rune_literal()
    {
        var tokens = LexContent("'a'");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.RuneLiteralToken, tokens[0].Kind);
    }

    // ----------------------------------------------------------------
    // Keywords classified correctly
    // ----------------------------------------------------------------

    [DataTestMethod]
    [DataRow("break", SyntaxKind.BreakKeyword)]
    [DataRow("case", SyntaxKind.CaseKeyword)]
    [DataRow("chan", SyntaxKind.ChanKeyword)]
    [DataRow("const", SyntaxKind.ConstKeyword)]
    [DataRow("continue", SyntaxKind.ContinueKeyword)]
    [DataRow("default", SyntaxKind.DefaultKeyword)]
    [DataRow("defer", SyntaxKind.DeferKeyword)]
    [DataRow("else", SyntaxKind.ElseKeyword)]
    [DataRow("fallthrough", SyntaxKind.FallthroughKeyword)]
    [DataRow("for", SyntaxKind.ForKeyword)]
    [DataRow("func", SyntaxKind.FuncKeyword)]
    [DataRow("go", SyntaxKind.GoKeyword)]
    [DataRow("goto", SyntaxKind.GotoKeyword)]
    [DataRow("if", SyntaxKind.IfKeyword)]
    [DataRow("import", SyntaxKind.ImportKeyword)]
    [DataRow("interface", SyntaxKind.InterfaceKeyword)]
    [DataRow("map", SyntaxKind.MapKeyword)]
    [DataRow("package", SyntaxKind.PackageKeyword)]
    [DataRow("range", SyntaxKind.RangeKeyword)]
    [DataRow("return", SyntaxKind.ReturnKeyword)]
    [DataRow("select", SyntaxKind.SelectKeyword)]
    [DataRow("struct", SyntaxKind.StructKeyword)]
    [DataRow("switch", SyntaxKind.SwitchKeyword)]
    [DataRow("type", SyntaxKind.TypeKeyword)]
    [DataRow("var", SyntaxKind.VarKeyword)]
    public void Keywords_are_classified(string text, SyntaxKind expected)
    {
        var tokens = LexContent(text);
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(expected, tokens[0].Kind);
        Assert.AreEqual(text, tokens[0].Text);
    }

    [TestMethod]
    public void Keyword_prefix_is_identifier()
    {
        // "functional" starts with "func" but is not a keyword
        var tokens = LexContent("functional");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[0].Kind);
    }

    // ----------------------------------------------------------------
    // Operators
    // ----------------------------------------------------------------

    [DataTestMethod]
    [DataRow("+", SyntaxKind.PlusToken)]
    [DataRow("-", SyntaxKind.MinusToken)]
    [DataRow("*", SyntaxKind.StarToken)]
    [DataRow("/", SyntaxKind.SlashToken)]
    [DataRow("%", SyntaxKind.PercentToken)]
    [DataRow("&", SyntaxKind.AmpersandToken)]
    [DataRow("|", SyntaxKind.PipeToken)]
    [DataRow("^", SyntaxKind.CaretToken)]
    [DataRow("<<", SyntaxKind.LessThanLessThanToken)]
    [DataRow(">>", SyntaxKind.GreaterThanGreaterThanToken)]
    [DataRow("&^", SyntaxKind.AmpersandCaretToken)]
    [DataRow("+=", SyntaxKind.PlusEqualsToken)]
    [DataRow("-=", SyntaxKind.MinusEqualsToken)]
    [DataRow("*=", SyntaxKind.StarEqualsToken)]
    [DataRow("/=", SyntaxKind.SlashEqualsToken)]
    [DataRow("%=", SyntaxKind.PercentEqualsToken)]
    [DataRow("&=", SyntaxKind.AmpersandEqualsToken)]
    [DataRow("|=", SyntaxKind.PipeEqualsToken)]
    [DataRow("^=", SyntaxKind.CaretEqualsToken)]
    [DataRow("<<=", SyntaxKind.LessThanLessThanEqualsToken)]
    [DataRow(">>=", SyntaxKind.GreaterThanGreaterThanEqualsToken)]
    [DataRow("&^=", SyntaxKind.AmpersandCaretEqualsToken)]
    [DataRow("&&", SyntaxKind.AmpersandAmpersandToken)]
    [DataRow("||", SyntaxKind.PipePipeToken)]
    [DataRow("<-", SyntaxKind.LessThanMinusToken)]
    [DataRow("++", SyntaxKind.PlusPlusToken)]
    [DataRow("--", SyntaxKind.MinusMinusToken)]
    [DataRow("==", SyntaxKind.EqualsEqualsToken)]
    [DataRow("!=", SyntaxKind.ExclamationEqualsToken)]
    [DataRow("<", SyntaxKind.LessThanToken)]
    [DataRow(">", SyntaxKind.GreaterThanToken)]
    [DataRow("<=", SyntaxKind.LessThanEqualsToken)]
    [DataRow(">=", SyntaxKind.GreaterThanEqualsToken)]
    [DataRow("=", SyntaxKind.EqualsToken)]
    [DataRow(":=", SyntaxKind.ColonEqualsToken)]
    [DataRow("!", SyntaxKind.ExclamationToken)]
    [DataRow(".", SyntaxKind.DotToken)]
    [DataRow("...", SyntaxKind.EllipsisToken)]
    [DataRow(",", SyntaxKind.CommaToken)]
    [DataRow(";", SyntaxKind.SemicolonToken)]
    [DataRow(":", SyntaxKind.ColonToken)]
    [DataRow("(", SyntaxKind.OpenParenToken)]
    [DataRow(")", SyntaxKind.CloseParenToken)]
    [DataRow("{", SyntaxKind.OpenBraceToken)]
    [DataRow("}", SyntaxKind.CloseBraceToken)]
    [DataRow("[", SyntaxKind.OpenBracketToken)]
    [DataRow("]", SyntaxKind.CloseBracketToken)]
    public void Operators_and_punctuation(string text, SyntaxKind expected)
    {
        var tokens = LexContent(text);
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(expected, tokens[0].Kind);
    }

    // ----------------------------------------------------------------
    // Extra
    // ----------------------------------------------------------------

    [TestMethod]
    public void Leading_whitespace_is_trivia()
    {
        var tokens = LexContent("   x");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("x", tokens[0].Text);
        Assert.AreEqual(1, tokens[0].LeadingExtra.Count);
        Assert.AreEqual(SyntaxKind.WhitespaceExtra, tokens[0].LeadingExtra[0].Kind);
        Assert.AreEqual("   ", tokens[0].LeadingExtra[0].Text);
    }

    [TestMethod]
    public void Trailing_whitespace_is_trivia()
    {
        var tokens = Lex("x   ");
        // x + EOF
        var x = tokens[0];
        Assert.AreEqual("x", x.Text);
        Assert.AreEqual(1, x.TrailingExtra.Count);
        Assert.AreEqual(SyntaxKind.WhitespaceExtra, x.TrailingExtra[0].Kind);
    }

    [TestMethod]
    public void Line_comment_is_trailing_trivia()
    {
        var tokens = Lex("x // comment\ny");
        // x ; y EOF (semicolon auto-inserted after x before newline)
        var x = tokens[0];
        Assert.AreEqual("x", x.Text);
        // Trailing trivia should contain whitespace + comment + newline
        Assert.IsTrue(x.TrailingExtra.Count >= 1);
        Assert.IsTrue(x.TrailingExtra.Any(t => t.Kind == SyntaxKind.LineCommentExtra));
    }

    [TestMethod]
    public void Block_comment_is_trivia()
    {
        var tokens = Lex("/* comment */ x");
        var x = tokens.First(t => t.Kind == SyntaxKind.IdentifierToken);
        Assert.AreEqual("x", x.Text);
        Assert.IsTrue(x.LeadingExtra.Any(t => t.Kind == SyntaxKind.BlockCommentExtra));
    }

    [TestMethod]
    public void Whitespace_between_tokens_is_trivia()
    {
        var tokens = LexContent("x + y");
        Assert.AreEqual(3, tokens.Count);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.PlusToken, tokens[1].Kind);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[2].Kind);
        // Space before + is trailing trivia of x
        Assert.IsTrue(tokens[0].TrailingExtra.Any(t => t.Kind == SyntaxKind.WhitespaceExtra));
        // Space after + is trailing trivia of +
        Assert.IsTrue(tokens[1].TrailingExtra.Any(t => t.Kind == SyntaxKind.WhitespaceExtra));
    }

    // ----------------------------------------------------------------
    // Auto-semicolon insertion
    // ----------------------------------------------------------------

    [TestMethod]
    public void Auto_semicolon_after_identifier()
    {
        var tokens = LexContent("x\ny");
        // Should be: x y (auto-semicolons filtered by LexContent)
        // Use LexNonEof to see the semicolons
        var all = LexNonEof("x\ny");
        Assert.IsTrue(all.Count >= 3);
        Assert.AreEqual(SyntaxKind.IdentifierToken, all[0].Kind);
        Assert.AreEqual(SyntaxKind.SemicolonToken, all[1].Kind);
        Assert.AreEqual(SyntaxKind.IdentifierToken, all[2].Kind);
    }

    [TestMethod]
    public void Auto_semicolon_after_return()
    {
        var all = LexNonEof("return\nx");
        Assert.IsTrue(all.Count >= 3);
        Assert.AreEqual(SyntaxKind.ReturnKeyword, all[0].Kind);
        Assert.AreEqual(SyntaxKind.SemicolonToken, all[1].Kind);
        Assert.AreEqual(SyntaxKind.IdentifierToken, all[2].Kind);
    }

    [TestMethod]
    public void Auto_semicolon_after_close_paren()
    {
        var all = LexNonEof(")\n{");
        Assert.IsTrue(all.Count >= 3);
        Assert.AreEqual(SyntaxKind.CloseParenToken, all[0].Kind);
        Assert.AreEqual(SyntaxKind.SemicolonToken, all[1].Kind);
        Assert.AreEqual(SyntaxKind.OpenBraceToken, all[2].Kind);
    }

    [TestMethod]
    public void Auto_semicolon_after_integer()
    {
        var all = LexNonEof("42\nx");
        Assert.IsTrue(all.Count >= 3);
        Assert.AreEqual(SyntaxKind.IntLiteralToken, all[0].Kind);
        Assert.AreEqual(SyntaxKind.SemicolonToken, all[1].Kind);
    }

    [TestMethod]
    public void No_auto_semicolon_after_open_brace()
    {
        var tokens = LexContent("{\nx");
        // Should be: { x (no semicolon after {)
        Assert.AreEqual(2, tokens.Count);
        Assert.AreEqual(SyntaxKind.OpenBraceToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[1].Kind);
    }

    [TestMethod]
    public void No_auto_semicolon_after_comma()
    {
        var tokens = LexContent(",\nx");
        Assert.AreEqual(2, tokens.Count);
        Assert.AreEqual(SyntaxKind.CommaToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[1].Kind);
    }

    [TestMethod]
    public void Auto_semicolon_at_eof()
    {
        var tokens = Lex("x");
        // Should be: x ; EOF
        Assert.AreEqual(3, tokens.Count);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.SemicolonToken, tokens[1].Kind);
        Assert.AreEqual(SyntaxKind.EndOfFileToken, tokens[2].Kind);
    }

    [TestMethod]
    public void Auto_semicolon_after_break()
    {
        var all = LexNonEof("break\n");
        Assert.IsTrue(all.Count >= 2);
        Assert.AreEqual(SyntaxKind.BreakKeyword, all[0].Kind);
        Assert.AreEqual(SyntaxKind.SemicolonToken, all[1].Kind);
    }

    [TestMethod]
    public void Auto_semicolon_after_string_literal()
    {
        var all = LexNonEof("\"hello\"\nx");
        Assert.IsTrue(all.Count >= 3);
        Assert.AreEqual(SyntaxKind.StringLiteralToken, all[0].Kind);
        Assert.AreEqual(SyntaxKind.SemicolonToken, all[1].Kind);
    }

    // ----------------------------------------------------------------
    // Multi-token sequences
    // ----------------------------------------------------------------

    [TestMethod]
    public void Short_var_declaration()
    {
        var tokens = LexContent("x := 42");
        Assert.AreEqual(3, tokens.Count);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.ColonEqualsToken, tokens[1].Kind);
        Assert.AreEqual(SyntaxKind.IntLiteralToken, tokens[2].Kind);
    }

    [TestMethod]
    public void Package_declaration()
    {
        var tokens = LexContent("package main");
        Assert.AreEqual(2, tokens.Count);
        Assert.AreEqual(SyntaxKind.PackageKeyword, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[1].Kind);
        Assert.AreEqual("main", tokens[1].Text);
    }

    [TestMethod]
    public void Function_call()
    {
        var tokens = LexContent("fmt.Println(\"hello\")");
        Assert.AreEqual(6, tokens.Count);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.DotToken, tokens[1].Kind);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[2].Kind);
        Assert.AreEqual(SyntaxKind.OpenParenToken, tokens[3].Kind);
        Assert.AreEqual(SyntaxKind.StringLiteralToken, tokens[4].Kind);
        Assert.AreEqual(SyntaxKind.CloseParenToken, tokens[5].Kind);
    }

    // ----------------------------------------------------------------
    // Position tracking
    // ----------------------------------------------------------------

    [TestMethod]
    public void Token_positions_are_correct()
    {
        var tokens = LexContent("x + y");
        Assert.AreEqual(0, tokens[0].Position); // x at 0
        Assert.AreEqual(2, tokens[1].Position); // + at 2
        Assert.AreEqual(4, tokens[2].Position); // y at 4
    }

    [TestMethod]
    public void Token_spans_are_correct()
    {
        var tokens = LexContent(":=");
        Assert.AreEqual(0, tokens[0].Span.Start);
        Assert.AreEqual(2, tokens[0].Span.Length);
    }

    // ----------------------------------------------------------------
    // Round-trip: all tokens + trivia reconstruct the source
    // ----------------------------------------------------------------

    [DataTestMethod]
    [DataRow("package main")]
    [DataRow("x := 42")]
    [DataRow("func main() {\n}")]
    [DataRow("// comment\npackage main")]
    [DataRow("x + y * z")]
    [DataRow("fmt.Println(\"hello\")")]
    public void Round_trip_preserves_source(string source)
    {
        var tokens = Lex(source);
        var reconstructed = new System.Text.StringBuilder();

        foreach (var token in tokens)
        {
            foreach (var trivia in token.LeadingExtra)
                reconstructed.Append(trivia.Text);

            reconstructed.Append(token.Text);

            foreach (var trivia in token.TrailingExtra)
                reconstructed.Append(trivia.Text);
        }

        Assert.AreEqual(source, reconstructed.ToString());
    }

    [TestMethod]
    public void Hex_float_with_exponent()
    {
        var tokens = LexContent("0x1p10");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.FloatLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Hex_float_with_fraction_and_exponent()
    {
        var tokens = LexContent("0x1.fp10");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.FloatLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Hex_float_negative_exponent()
    {
        var tokens = LexContent("0xA.Bp-3");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.FloatLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Hex_float_positive_exponent()
    {
        var tokens = LexContent("0x1P+5");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.FloatLiteralToken, tokens[0].Kind);
    }

    // ----------------------------------------------------------------
    // Multi-char escape sequences in rune and string literals
    // ----------------------------------------------------------------

    [TestMethod]
    public void Rune_hex_escape()
    {
        var tokens = LexContent("'\\x41'");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.RuneLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Rune_unicode_escape_u()
    {
        var tokens = LexContent("'\\u0041'");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.RuneLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Rune_unicode_escape_U()
    {
        var tokens = LexContent("'\\U00000041'");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.RuneLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void Rune_octal_escape()
    {
        var tokens = LexContent("'\\077'");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.RuneLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void String_hex_escapes()
    {
        var tokens = LexContent("\"\\x48\\x69\"");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.StringLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void String_unicode_escape()
    {
        var tokens = LexContent("\"\\u0048\\u0065\\u006C\\u006C\\u006F\"");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.StringLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void String_big_unicode_escape()
    {
        var tokens = LexContent("\"\\U00000048\\U00000069\"");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.StringLiteralToken, tokens[0].Kind);
    }

    [TestMethod]
    public void String_octal_escape()
    {
        var tokens = LexContent("\"\\110\\145\\154\\154\\157\"");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.StringLiteralToken, tokens[0].Kind);
    }
}
