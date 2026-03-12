// -----------------------------------------------------------------------
// <copyright file="GoDotnet.cs" company="Ziad">
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

using System;
using System.Collections.Generic;
using System.Reflection;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime
{
    [GoPackage("dotnet")]
    public static class GoDotnet
    {
        private static readonly Dictionary<string, Type> _typeCache = new();

        public static Type? ResolveType(string typeName)
        {
            if (_typeCache.TryGetValue(typeName, out var cached))
                return cached;

            // Try common assemblies
            var type = Type.GetType(typeName)
                ?? Type.GetType(typeName + ", System.Runtime")
                ?? Type.GetType(typeName + ", System.Console")
                ?? Type.GetType(typeName + ", System.IO.FileSystem")
                ?? Type.GetType(typeName + ", System.Net.Http")
                ?? Type.GetType(typeName + ", System.Collections");

            if (type != null)
                _typeCache[typeName] = type;

            return type;
        }

        public static object? CallStatic(string typeName, string methodName, params object?[] args)
        {
            var type = ResolveType(typeName);
            if (type == null)
                throw new InvalidOperationException($"dotnet: type '{typeName}' not found");

            var argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                argTypes[i] = args[i]?.GetType() ?? typeof(object);

            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, argTypes);
            if (method == null)
            {
                // Fallback: try by name and parameter count
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name == methodName && m.GetParameters().Length == args.Length)
                    {
                        method = m;
                        break;
                    }
                }
            }

            if (method == null)
                throw new InvalidOperationException(
                    $"dotnet: method '{methodName}' not found on type '{typeName}'");

            return method.Invoke(null, args);
        }

        public static object? GetStaticProperty(string typeName, string propertyName)
        {
            var type = ResolveType(typeName);
            if (type == null)
                throw new InvalidOperationException($"dotnet: type '{typeName}' not found");

            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (prop != null)
                return prop.GetValue(null);

            var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (field != null)
                return field.GetValue(null);

            throw new InvalidOperationException(
                $"dotnet: property/field '{propertyName}' not found on type '{typeName}'");
        }

        public static object New(string typeName, params object?[] args)
        {
            var type = ResolveType(typeName);
            if (type == null)
                throw new InvalidOperationException($"dotnet: type '{typeName}' not found");

            return Activator.CreateInstance(type, args)!;
        }

        public static object? CallMethod(object instance, string methodName, params object?[] args)
        {
            var type = instance.GetType();

            var argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                argTypes[i] = args[i]?.GetType() ?? typeof(object);

            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, argTypes);
            if (method == null)
            {
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name == methodName && m.GetParameters().Length == args.Length)
                    {
                        method = m;
                        break;
                    }
                }
            }

            if (method == null)
                throw new InvalidOperationException(
                    $"dotnet: method '{methodName}' not found on type '{type.FullName}'");

            return method.Invoke(instance, args);
        }

        public static object? GetProperty(object instance, string propertyName)
        {
            var type = instance.GetType();
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
                return prop.GetValue(instance);

            var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(instance);

            throw new InvalidOperationException(
                $"dotnet: property/field '{propertyName}' not found on type '{type.FullName}'");
        }

        public static void SetProperty(object instance, string propertyName, object? value)
        {
            var type = instance.GetType();
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                prop.SetValue(instance, value);
                return;
            }

            var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new InvalidOperationException(
                $"dotnet: property/field '{propertyName}' not found on type '{type.FullName}'");
        }

        public static string TypeName(object? instance)
        {
            return instance?.GetType().FullName ?? "nil";
        }
    }
}
