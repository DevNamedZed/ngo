using System;

namespace Ngo.Runtime.Discovery
{
    /// <summary>
    /// Specifies the Go return type(s) for a method.
    /// Only needed when the Go return types differ from the C# return type mapping
    /// (e.g. C# (long, object?) → Go (int, error)).
    /// </summary>
    [AttributeUsage(AttributeTargets.ReturnValue, AllowMultiple = false)]
    public sealed class GoReturnAttribute : Attribute
    {
        /// <summary>Go return type strings.</summary>
        public string[] Types { get; }

        public GoReturnAttribute(params string[] types)
        {
            Types = types;
        }
    }
}
