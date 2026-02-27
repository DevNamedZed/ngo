// -----------------------------------------------------------------------
// <copyright file="GoMath.cs" company="Ziad">
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
    public static class GoMath
    {
        public static double Abs(double x) => Math.Abs(x);
        public static double Max(double x, double y) => Math.Max(x, y);
        public static double Min(double x, double y) => Math.Min(x, y);
        public static double Sqrt(double x) => Math.Sqrt(x);
        public static double Floor(double x) => Math.Floor(x);
        public static double Ceil(double x) => Math.Ceiling(x);
        public static double Round(double x) => Math.Round(x);
        public static double Pow(double x, double y) => Math.Pow(x, y);
        public static double Log(double x) => Math.Log(x);
        public static double Mod(double x, double y) => x % y;
    }
}
