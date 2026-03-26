using System;

namespace Ngo.Runtime.Discovery
{
    /// <summary>
    /// Marks a C# class/struct as a Go type. Placed on the actual type definition.
    /// Kind: "struct", "interface", or "named".
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false)]
    public sealed class GoTypeAttribute : Attribute
    {
        /// <summary>Go type name. Null means use the C# type name.</summary>
        public string? Name { get; set; }

        /// <summary>"struct", "interface", or "named".</summary>
        public string Kind { get; }

        /// <summary>For named types, the underlying Go type string.</summary>
        public string? Underlying { get; set; }

        /// <summary>Go import path of the package this type belongs to.</summary>
        public string? Package { get; set; }

        /// <summary>
        /// When true, this C# class represents a Go pointer-to-struct (*T) rather than
        /// a struct value (T). Use for Go types that are always used via pointer receivers
        /// and pointer parameters (e.g. big.Int, where Go code uses *big.Int everywhere).
        /// When false (default), the C# class represents the Go struct value directly.
        /// </summary>
        public bool Pointer { get; set; }

        /// <summary>Comma-separated type parameter names for generic types (e.g., "T" or "K,V").</summary>
        public string? TypeParams { get; set; }

        public GoTypeAttribute(string kind)
        {
            Kind = kind;
        }
    }
}
