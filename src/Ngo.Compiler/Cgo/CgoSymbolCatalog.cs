using System.Collections.Generic;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Normalised snapshot of every C symbol reachable through a
    /// compiled probe. Populated by an <see cref="ICgoSymbolSource"/>
    /// and consumed by the P/Invoke emitter and marshalling-stub
    /// generator. The catalog is reader-agnostic: DWARF and PDB
    /// readers produce the same shape, so a bug in either never
    /// escapes its file.
    ///
    /// Each symbol kind lives in its own name-keyed dictionary so
    /// downstream resolution does not iterate lists. Write access
    /// is gated through <c>Add*</c> methods; last write wins for
    /// duplicate names within a single reader pass, which matches
    /// how debug info presents transitively-included headers.
    /// </summary>
    public sealed class CgoSymbolCatalog
    {
        private readonly Dictionary<string, CgoTypedefInfo> _typedefs = new();
        private readonly Dictionary<string, CgoStructInfo> _structsAndUnions = new();
        private readonly Dictionary<string, CgoEnumInfo> _enums = new();
        private readonly Dictionary<string, CgoFunctionInfo> _functions = new();
        private readonly Dictionary<string, CgoFunctionPointerInfo> _functionPointers = new();
        private readonly Dictionary<string, CgoOpaqueTypeInfo> _opaqueTypes = new();
        private readonly Dictionary<string, CgoMacroConstantInfo> _macroConstants = new();

        public IReadOnlyDictionary<string, CgoTypedefInfo> Typedefs
        {
            get { return _typedefs; }
        }

        /// <summary>
        /// Both structs and unions live here. The distinction is
        /// carried on <see cref="CgoStructInfo.IsUnion"/>.
        /// </summary>
        public IReadOnlyDictionary<string, CgoStructInfo> StructsAndUnions
        {
            get { return _structsAndUnions; }
        }

        public IReadOnlyDictionary<string, CgoEnumInfo> Enums
        {
            get { return _enums; }
        }

        public IReadOnlyDictionary<string, CgoFunctionInfo> Functions
        {
            get { return _functions; }
        }

        public IReadOnlyDictionary<string, CgoFunctionPointerInfo> FunctionPointers
        {
            get { return _functionPointers; }
        }

        public IReadOnlyDictionary<string, CgoOpaqueTypeInfo> OpaqueTypes
        {
            get { return _opaqueTypes; }
        }

        public IReadOnlyDictionary<string, CgoMacroConstantInfo> MacroConstants
        {
            get { return _macroConstants; }
        }

        public void AddTypedef(CgoTypedefInfo typedef)
        {
            _typedefs[typedef.Name] = typedef;
        }

        public void AddStructOrUnion(CgoStructInfo structOrUnion)
        {
            _structsAndUnions[structOrUnion.GoName] = structOrUnion;
        }

        public void AddEnum(CgoEnumInfo enumInfo)
        {
            _enums[enumInfo.Name] = enumInfo;
        }

        public void AddFunction(CgoFunctionInfo function)
        {
            _functions[function.Name] = function;
        }

        public void AddFunctionPointer(CgoFunctionPointerInfo functionPointer)
        {
            _functionPointers[functionPointer.Name] = functionPointer;
        }

        public void AddOpaqueType(CgoOpaqueTypeInfo opaqueType)
        {
            _opaqueTypes[opaqueType.Name] = opaqueType;
        }

        public void AddMacroConstant(CgoMacroConstantInfo constant)
        {
            _macroConstants[constant.Name] = constant;
        }
    }
}
