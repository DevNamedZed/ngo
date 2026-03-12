using System;

namespace Ngo.Runtime.Discovery
{
    /// <summary>
    /// Assembly-level attribute that maps a .NET namespace to a Go import path.
    /// Used by RuntimePackageResolver to find package types in sub-namespaces.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class GoPackageMappingAttribute : Attribute
    {
        public string Namespace { get; }
        public string ImportPath { get; }

        public GoPackageMappingAttribute(string clrNamespace, string goImportPath)
        {
            Namespace = clrNamespace;
            ImportPath = goImportPath;
        }
    }
}
