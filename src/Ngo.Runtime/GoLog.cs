// -----------------------------------------------------------------------
// <copyright file="GoLog.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public static class GoLog
    {
        // log.Println(v ...interface{})
        public static void Println(params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) Console.Error.Write(" ");
                Console.Error.Write(BuiltIn.FormatArg(args[i]));
            }
            Console.Error.WriteLine();
        }

        // log.Print(v ...interface{})
        public static void Print(params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) Console.Error.Write(" ");
                Console.Error.Write(BuiltIn.FormatArg(args[i]));
            }
        }

        // log.Printf(format string, v ...interface{})
        public static void Printf(string format, params object[] args)
        {
            var result = Fmt.Sprintf(format, args);
            Console.Error.Write(result);
        }

        // log.Fatal(v ...interface{})
        public static void Fatal(params object[] args)
        {
            Println(args);
            Environment.Exit(1);
        }

        // log.Fatalf(format string, v ...interface{})
        public static void Fatalf(string format, params object[] args)
        {
            Printf(format, args);
            Console.Error.WriteLine();
            Environment.Exit(1);
        }
    }
}
