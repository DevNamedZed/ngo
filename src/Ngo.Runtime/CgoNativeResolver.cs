using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Ngo.Runtime
{
    public static class CgoNativeResolver
    {
        private static bool _registered;

        public static void Register(Assembly assembly)
        {
            if (_registered) return;
            _registered = true;
            NativeLibrary.SetDllImportResolver(assembly, ResolveNativeLibrary);
        }

        private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            var assemblyDir = System.IO.Path.GetDirectoryName(assembly.Location);
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                var libPath = System.IO.Path.Combine(assemblyDir, GetPlatformLibName(libraryName));
                if (NativeLibrary.TryLoad(libPath, out var handle))
                    return handle;
            }
            if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var defaultHandle))
                return defaultHandle;
            return IntPtr.Zero;
        }

        private static string GetPlatformLibName(string name)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return name.EndsWith(".dll") ? name : $"{name}.dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return name.StartsWith("lib") ? $"{name}.dylib" : $"lib{name}.dylib";
            return name.StartsWith("lib") ? $"{name}.so" : $"lib{name}.so";
        }
    }
}
