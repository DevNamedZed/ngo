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
        public static object? TypeOf(object? v)
        {
            if (v == null)
            {
                return null;
            }
            return Reflect.GoReflect.TypeOf(v);
        }

        // reflectlite.ValueOf(v interface{}) Value
        [GoFunc]
        [return: GoReturn("reflectlite.Value")]
        public static GoValue ValueOf(object? v)
        {
            return new GoValue(v);
        }

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
        public static Action<long, long> Swapper(object? slice)
        {
            return Reflect.GoReflect.Swapper(slice);
        }

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
        private readonly object? _value;

        public GoValue() { _value = null; }

        internal GoValue(object? value)
        {
            _value = value;
        }

        [GoMethod]
        [return: GoReturn("reflectlite.Kind")]
        public long Kind()
        {
            if (_value == null)
            {
                return (long)GoReflectKinds.Invalid;
            }
            return GoReflectType.DeriveKind(_value.GetType());
        }

        [GoMethod]
        [return: GoReturn("reflectlite.Type")]
        public object? Type()
        {
            if (_value == null)
            {
                return null;
            }
            return Reflect.GoReflect.TypeOf(_value);
        }

        [GoMethod]
        public bool IsValid() => _value != null;

        [GoMethod]
        public bool IsNil()
        {
            return _value == null;
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Len()
        {
            if (_value == null)
            {
                return 0;
            }
            var lenProp = _value.GetType().GetProperty("Len");
            if (lenProp != null)
            {
                return Convert.ToInt64(lenProp.GetValue(_value));
            }
            if (_value is string s)
            {
                return s.Length;
            }
            if (_value is Array arr)
            {
                return arr.Length;
            }
            return 0;
        }

        [GoMethod]
        [return: GoReturn("reflectlite.Value")]
        public GoValue Elem()
        {
            if (_value == null)
            {
                return new GoValue();
            }
            // For Ptr types, unwrap
            var type = _value.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Ptr<>))
            {
                var field = type.GetField("Value");
                if (field != null)
                {
                    return new GoValue(field.GetValue(_value));
                }
            }
            return new GoValue(_value);
        }

        [GoMethod]
        public void Set(GoValue x) { }
    }
}
