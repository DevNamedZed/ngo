using System;

namespace Ngo.Runtime.Discovery
{
    /// <summary>
    /// Marks a static class as implementing a Go stdlib package.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class GoPackageAttribute : Attribute
    {
        public string ImportPath { get; }

        public GoPackageAttribute(string importPath)
        {
            ImportPath = importPath;
        }
    }
}
