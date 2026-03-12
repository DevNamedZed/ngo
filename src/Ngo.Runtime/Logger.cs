using System;
using System.IO;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime
{
    [GoType("struct", Name = "Logger", Package = "log")]
    public sealed class Logger
    {
        private object _output;
        private string _prefix;
        private long _flags;

        public Logger(object output, string prefix, long flags)
        {
            _output = output;
            _prefix = prefix;
            _flags = flags;
        }

        [GoMethod(IsVariadic = true)]
        public void Println([GoParam("interface{}")] params object[] args)
        {
            var w = GetWriter();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) w.Write(" ");
                w.Write(BuiltIn.FormatArg(args[i]));
            }
            w.WriteLine();
            w.Flush();
        }

        [GoMethod(IsVariadic = true)]
        public void Printf(string format, [GoParam("interface{}")] params object[] args)
        {
            var w = GetWriter();
            var result = Fmt.Package.Sprintf(format, args);
            w.Write(result);
            w.Flush();
        }

        [GoMethod(IsVariadic = true)]
        public void Print([GoParam("interface{}")] params object[] args)
        {
            var w = GetWriter();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) w.Write(" ");
                w.Write(BuiltIn.FormatArg(args[i]));
            }
            w.Flush();
        }

        [GoMethod(IsVariadic = true)]
        public void Fatal([GoParam("interface{}")] params object[] args)
        {
            Println(args);
            Environment.Exit(1);
        }

        [GoMethod(IsVariadic = true)]
        public void Fatalf(string format, [GoParam("interface{}")] params object[] args)
        {
            Printf(format, args);
            GetWriter().WriteLine();
            GetWriter().Flush();
            Environment.Exit(1);
        }

        [GoMethod(IsVariadic = true)]
        public void Fatalln([GoParam("interface{}")] params object[] args)
        {
            Println(args);
            Environment.Exit(1);
        }

        [GoMethod(IsVariadic = true)]
        public void Panic([GoParam("interface{}")] params object[] args)
        {
            var s = Ngo.Runtime.Log.Package.FormatArgs(args);
            var w = GetWriter();
            w.Write(s);
            w.Flush();
            throw new GoPanicException(s);
        }

        [GoMethod(IsVariadic = true)]
        public void Panicf(string format, [GoParam("interface{}")] params object[] args)
        {
            var s = Fmt.Package.Sprintf(format, args);
            var w = GetWriter();
            w.Write(s);
            w.Flush();
            throw new GoPanicException(s);
        }

        [GoMethod(IsVariadic = true)]
        public void Panicln([GoParam("interface{}")] params object[] args)
        {
            var s = Ngo.Runtime.Log.Package.FormatArgs(args);
            var w = GetWriter();
            w.WriteLine(s);
            w.Flush();
            throw new GoPanicException(s);
        }

        [GoMethod]
        public void SetOutput([GoParam("io.Writer")] object w)
        {
            _output = w;
        }

        [GoMethod]
        public void SetPrefix(string prefix)
        {
            _prefix = prefix;
        }

        [GoMethod]
        public void SetFlags([GoParam("int")] long flag)
        {
            _flags = flag;
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Flags()
        {
            return _flags;
        }

        [GoMethod]
        public string Prefix()
        {
            return _prefix;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Output([GoParam("int")] long calldepth, string s)
        {
            try
            {
                var w = GetWriter();
                w.Write(s);
                w.Flush();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        internal object WriterValue() => _output;

        private TextWriter GetWriter()
        {
            if (_output is TextWriter tw) return tw;
            return Console.Error;
        }
    }
}
