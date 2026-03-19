using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Types.Errors
{
    /// <summary>
    /// Stub for internal/types/errors — error codes for go/types.
    /// </summary>
    [GoPackage("internal/types/errors")]
    public static class Package
    {
        // Error code constants — type-checking error codes
        [GoConst] public const long InvalidSyntaxTree = 0;
        [GoConst] public const long Test = -1;

        // Code is a type alias for int
        [GoType("named", Name = "Code", Package = "internal/types/errors", Underlying = "int")]
        public struct GoCodeType { }

        // InvalidPkgUse and other error codes
        [GoConst] public const long BlankPkgName = 1;
        [GoConst] public const long MismatchedPkgName = 2;
        [GoConst] public const long InvalidPkgUse = 3;
        [GoConst] public const long BadImportPath = 4;
        [GoConst] public const long BrokenImport = 5;
        [GoConst] public const long ImportCRenamed = 6;
        [GoConst] public const long UnusedImport = 7;
        [GoConst] public const long InvalidInitCycle = 8;
        [GoConst] public const long DuplicateDecl = 9;
        [GoConst] public const long InvalidDeclCycle = 10;
        [GoConst] public const long InvalidTypeCycle = 11;
        [GoConst] public const long InvalidConstInit = 12;
        [GoConst] public const long InvalidConstVal = 13;
        [GoConst] public const long InvalidConstType = 14;
        [GoConst] public const long UntypedNilUse = 15;
        [GoConst] public const long WrongAssignCount = 16;
        [GoConst] public const long UnassignableOperand = 17;
        [GoConst] public const long NoNewVar = 18;
        [GoConst] public const long MultiValAssignOp = 19;
        [GoConst] public const long InvalidIfaceAssign = 20;
        [GoConst] public const long InvalidChanAssign = 21;
        [GoConst] public const long IncompatibleAssign = 22;
        [GoConst] public const long UnaddressableFieldAssign = 23;
        [GoConst] public const long NotAType = 24;
        [GoConst] public const long InvalidArrayLen = 25;
        [GoConst] public const long BlankIfaceMethod = 26;
        [GoConst] public const long IncomparableMapKey = 27;
        [GoConst] public const long InvalidIfaceEmbed = 28;
        [GoConst] public const long InvalidPtrEmbed = 29;
        [GoConst] public const long BadRecv = 30;
        [GoConst] public const long InvalidRecv = 31;
        [GoConst] public const long DuplicateFieldAndMethod = 32;
        [GoConst] public const long DuplicateMethod = 33;
        [GoConst] public const long InvalidBlank = 34;
        [GoConst] public const long InvalidIota = 35;
        [GoConst] public const long MissingInitBody = 36;
        [GoConst] public const long InvalidInitSig = 37;
        [GoConst] public const long InvalidInitDecl = 38;
        [GoConst] public const long InvalidMainDecl = 39;
        [GoConst] public const long TooManyValues = 40;
        [GoConst] public const long NotAnExpr = 41;
        [GoConst] public const long TruncatedFloat = 42;
        [GoConst] public const long NumericOverflow = 43;
        [GoConst] public const long UndefinedOp = 44;
        [GoConst] public const long MismatchedTypes = 45;
        [GoConst] public const long DivByZero = 46;
        [GoConst] public const long NonNumericIncDec = 47;
        [GoConst] public const long UnaddressableOperand = 48;
        [GoConst] public const long InvalidIndirection = 49;
        [GoConst] public const long NonIndexableOperand = 50;
        [GoConst] public const long InvalidIndex = 51;
        [GoConst] public const long SwappedSliceIndices = 52;
        [GoConst] public const long NonSliceableOperand = 53;
        [GoConst] public const long InvalidSliceExpr = 54;
        [GoConst] public const long InvalidShiftCount = 55;
        [GoConst] public const long InvalidShiftOperand = 56;
        [GoConst] public const long InvalidReceive = 57;
        [GoConst] public const long InvalidSend = 58;
        [GoConst] public const long DuplicateLitKey = 59;
        [GoConst] public const long MissingLitKey = 60;
        [GoConst] public const long InvalidLitIndex = 61;
        [GoConst] public const long OversizeCompLit = 62;
        [GoConst] public const long MixedStructLit = 63;
        [GoConst] public const long InvalidStructLit = 64;
        [GoConst] public const long MissingLitField = 65;
        [GoConst] public const long DuplicateLitField = 66;
        [GoConst] public const long UnexportedLitField = 67;
        [GoConst] public const long InvalidLitField = 68;
        [GoConst] public const long UntypedLit = 69;
        [GoConst] public const long InvalidLit = 70;
        [GoConst] public const long AmbiguousSelector = 71;
        [GoConst] public const long UndeclaredImportedName = 72;
        [GoConst] public const long UnexportedName = 73;
        [GoConst] public const long UndeclaredName = 74;
        [GoConst] public const long MissingFieldOrMethod = 75;
    }
}
