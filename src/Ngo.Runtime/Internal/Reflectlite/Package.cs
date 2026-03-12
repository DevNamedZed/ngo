using System;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Reflect;

namespace Ngo.Runtime.Internal.Reflectlite
{
    [GoPackage("internal/reflectlite")]
    public static class Package
    {
        // reflectlite.TypeOf(v interface{}) Type
        [GoFunc]
        [return: GoReturn("reflectlite.Type")]
        public static object? TypeOf(object? v) => null;

        // reflectlite.ValueOf(v interface{}) Value
        [GoFunc]
        [return: GoReturn("reflectlite.Value")]
        public static GoValue ValueOf(object? v) => new GoValue();

        // Kind constants (matching reflect.Kind)
        [GoVar(Type = "reflectlite.Kind")]
        public static readonly long Invalid = (long)GoReflectKinds.Invalid;

        [GoVar(Type = "reflectlite.Kind")]
        public static readonly long Ptr = (long)GoReflectKinds.Ptr;

        [GoVar(Type = "reflectlite.Kind")]
        public static readonly long Interface = (long)GoReflectKinds.Interface;

        // reflectlite.Swapper(slice interface{}) func(i, j int)
        [GoFunc]
        [return: GoReturn("func(int, int)")]
        public static Action<long, long> Swapper(object? slice) => (i, j) => { };

        // reflectlite.Type interface
        [GoType("interface", Name = "Type", Package = "internal/reflectlite")]
        public interface IType
        {
            [GoMethod]
            string Name();
            [GoMethod]
            [return: GoReturn("reflectlite.Kind")]
            long Kind();
            [GoMethod]
            string String();
            [GoMethod]
            bool Comparable();
            [GoMethod]
            object? Elem();
            [GoMethod]
            bool Implements(object? u);
            [GoMethod]
            bool AssignableTo(object? u);
        }
    }

    [GoType("struct", Name = "Value", Package = "internal/reflectlite")]
    public class GoValue
    {
        [GoMethod]
        [return: GoReturn("reflectlite.Kind")]
        public long Kind() => 0;

        [GoMethod]
        [return: GoReturn("reflectlite.Type")]
        public object? Type() => null;

        [GoMethod]
        public bool IsValid() => false;

        [GoMethod]
        public bool IsNil() => true;

        [GoMethod]
        [return: GoReturn("int")]
        public long Len() => 0;

        [GoMethod]
        [return: GoReturn("reflectlite.Value")]
        public GoValue Elem() => this;

        [GoMethod]
        public void Set(GoValue x) { }
    }
}
