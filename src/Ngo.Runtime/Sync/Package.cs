using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync
{
    [GoPackage("sync")]
    public static class Package
    {
        public static Cond NewCond(object? l) => Cond.NewCond(l);

        [GoFunc]
        public static Action OnceFunc(Action f)
        {
            var once = new Once();
            return () => once.Do(f);
        }

        [GoFunc]
        public static Func<T> OnceValue<T>(Func<T> f)
        {
            var once = new Once();
            T result = default!;
            return () =>
            {
                once.Do(() => { result = f(); });
                return result;
            };
        }

        [GoFunc]
        public static Func<(T1, T2)> OnceValues<T1, T2>(Func<(T1, T2)> f)
        {
            var once = new Once();
            T1 result1 = default!;
            T2 result2 = default!;
            return () =>
            {
                once.Do(() =>
                {
                    var (v1, v2) = f();
                    result1 = v1;
                    result2 = v2;
                });
                return (result1, result2);
            };
        }
    }
}
