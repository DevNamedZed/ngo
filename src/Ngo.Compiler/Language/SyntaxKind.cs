// -----------------------------------------------------------------------
// <copyright file="SyntaxKind.cs" company="Ziad">
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

namespace Ngo.Compiler.Language
{
    public enum SyntaxKind : ushort
    {
        None = 0,

        // ----------------------------------------------------------------
        // Extra kinds (1-99)
        // ----------------------------------------------------------------
        WhitespaceExtra = 1,
        EndOfLineExtra = 2,
        LineCommentExtra = 3,
        BlockCommentExtra = 4,

        // ----------------------------------------------------------------
        // Token kinds — Literals (100-149)
        // ----------------------------------------------------------------
        IdentifierToken = 100,
        IntLiteralToken = 101,
        FloatLiteralToken = 102,
        ImaginaryLiteralToken = 103,
        RuneLiteralToken = 104,
        StringLiteralToken = 105,
        RawStringLiteralToken = 106,

        // ----------------------------------------------------------------
        // Token kinds — Operators and punctuation (150-299)
        // ----------------------------------------------------------------

        // Arithmetic
        PlusToken = 150,            // +
        MinusToken = 151,           // -
        StarToken = 152,            // *
        SlashToken = 153,           // /
        PercentToken = 154,         // %

        // Bitwise
        AmpersandToken = 155,       // &
        PipeToken = 156,            // |
        CaretToken = 157,           // ^
        LessThanLessThanToken = 158,        // <<
        GreaterThanGreaterThanToken = 159,  // >>
        AmpersandCaretToken = 160,  // &^

        // Compound assignment
        PlusEqualsToken = 161,      // +=
        MinusEqualsToken = 162,     // -=
        StarEqualsToken = 163,      // *=
        SlashEqualsToken = 164,     // /=
        PercentEqualsToken = 165,   // %=
        AmpersandEqualsToken = 166, // &=
        PipeEqualsToken = 167,      // |=
        CaretEqualsToken = 168,     // ^=
        LessThanLessThanEqualsToken = 169,          // <<=
        GreaterThanGreaterThanEqualsToken = 170,    // >>=
        AmpersandCaretEqualsToken = 171,            // &^=

        // Logical
        AmpersandAmpersandToken = 172,  // &&
        PipePipeToken = 173,            // ||

        // Channel
        LessThanMinusToken = 174,   // <-

        // Increment/Decrement
        PlusPlusToken = 175,        // ++
        MinusMinusToken = 176,      // --

        // Comparison
        EqualsEqualsToken = 177,    // ==
        ExclamationEqualsToken = 178,   // !=
        LessThanToken = 179,        // <
        GreaterThanToken = 180,     // >
        LessThanEqualsToken = 181,  // <=
        GreaterThanEqualsToken = 182,   // >=

        // Assignment and declaration
        EqualsToken = 183,          // =
        ColonEqualsToken = 184,     // :=
        ExclamationToken = 185,     // !

        // Punctuation
        DotToken = 186,             // .
        EllipsisToken = 187,        // ...
        CommaToken = 188,           // ,
        SemicolonToken = 189,       // ;
        ColonToken = 190,           // :

        // Delimiters
        OpenParenToken = 191,       // (
        CloseParenToken = 192,      // )
        OpenBraceToken = 193,       // {
        CloseBraceToken = 194,      // }
        OpenBracketToken = 195,     // [
        CloseBracketToken = 196,    // ]

        // Special
        EndOfFileToken = 197,
        ErrorToken = 198,
        TildeToken = 199,           // ~

        // ----------------------------------------------------------------
        // Token kinds — Keywords (300-399)
        // ----------------------------------------------------------------
        BreakKeyword = 300,
        CaseKeyword = 301,
        ChanKeyword = 302,
        ConstKeyword = 303,
        ContinueKeyword = 304,
        DefaultKeyword = 305,
        DeferKeyword = 306,
        ElseKeyword = 307,
        FallthroughKeyword = 308,
        ForKeyword = 309,
        FuncKeyword = 310,
        GoKeyword = 311,
        GotoKeyword = 312,
        IfKeyword = 313,
        ImportKeyword = 314,
        InterfaceKeyword = 315,
        MapKeyword = 316,
        PackageKeyword = 317,
        RangeKeyword = 318,
        ReturnKeyword = 319,
        SelectKeyword = 320,
        StructKeyword = 321,
        SwitchKeyword = 322,
        TypeKeyword = 323,
        VarKeyword = 324,

        // ----------------------------------------------------------------
        // Node kinds — Top-level (1000-1099)
        // ----------------------------------------------------------------
        SourceFile = 1000,
        PackageClause = 1001,
        ImportDeclaration = 1002,
        ImportSpec = 1003,
        ImportSpecList = 1004,

        // ----------------------------------------------------------------
        // Node kinds — Declarations (1100-1199)
        // ----------------------------------------------------------------
        FunctionDeclaration = 1100,
        MethodDeclaration = 1101,
        TypeDeclaration = 1102,
        TypeSpec = 1103,
        VarDeclaration = 1104,
        VarSpec = 1105,
        ConstDeclaration = 1106,
        ConstSpec = 1107,
        ParameterList = 1108,
        Parameter = 1109,
        Receiver = 1110,
        ResultList = 1111,
        TypeParameterList = 1112,
        TypeParameterDecl = 1113,

        // ----------------------------------------------------------------
        // Node kinds — Statements (1200-1299)
        // ----------------------------------------------------------------
        Block = 1200,
        ReturnStatement = 1201,
        IfStatement = 1202,
        ForStatement = 1203,
        RangeClause = 1204,
        SwitchStatement = 1205,
        ExprSwitchCase = 1206,
        TypeSwitchStatement = 1207,
        TypeSwitchCase = 1208,
        SelectStatement = 1209,
        CommClause = 1210,
        AssignmentStatement = 1211,
        ShortVarDeclaration = 1212,
        IncDecStatement = 1213,
        SendStatement = 1214,
        GoStatement = 1215,
        DeferStatement = 1216,
        BranchStatement = 1217,     // break, continue, goto, fallthrough
        LabeledStatement = 1218,
        ExpressionStatement = 1219,
        EmptyStatement = 1220,

        // ----------------------------------------------------------------
        // Node kinds — Expressions (1300-1399)
        // ----------------------------------------------------------------
        BinaryExpression = 1300,
        UnaryExpression = 1301,
        CallExpression = 1302,
        IndexExpression = 1303,
        SliceExpression = 1304,
        SelectorExpression = 1305,
        TypeAssertExpression = 1306,
        CompositeLiteral = 1307,
        FunctionLiteral = 1308,
        ParenthesizedExpression = 1309,
        IdentifierName = 1310,
        LiteralExpression = 1311,
        KeyValuePair = 1312,
        ElementList = 1313,
        ArgumentList = 1314,
        TypeArgumentList = 1315,

        // ----------------------------------------------------------------
        // Node kinds — Types (1400-1499)
        // ----------------------------------------------------------------
        PredeclaredType = 1400,
        PointerType = 1401,
        ArrayType = 1402,
        SliceType = 1403,
        MapType = 1404,
        ChannelType = 1405,
        StructType = 1406,
        InterfaceType = 1407,
        FunctionType = 1408,
        QualifiedName = 1409,
        FieldDeclaration = 1410,
        MethodSpec = 1411,
        EmbeddedType = 1412,
        UnionType = 1413,
        UnionTerm = 1414,

        // ----------------------------------------------------------------
        // Node kinds — Error (1500+)
        // ----------------------------------------------------------------
        ErrorNode = 1500,
    }
}
