using Ngo.Runtime.Discovery;

namespace Ngo.Runtime
{
    [GoType("struct", Name = "NumError", Package = "strconv")]
    public class NumError
    {
        [GoField(Name = "Func")]
        public string Func;

        [GoField(Name = "Num")]
        public string Num;

        [GoField(Name = "Err", Type = "error")]
        public object? Err;

        public NumError(string func_, string num, object? err)
        {
            Func = func_;
            Num = num;
            Err = err;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Unwrap() => Err;

        [GoMethod]
        public string Error()
        {
            string e = Err?.ToString() ?? "";
            return $"strconv.{Func}: parsing \"{Num}\": {e}";
        }

        public override string ToString() => Error();
    }
}
