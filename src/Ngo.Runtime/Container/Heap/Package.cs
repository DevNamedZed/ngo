using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Container.Heap
{
    [GoPackage("container/heap")]
    public static class Package
    {
        // heap.Interface
        [GoType("interface", Name = "Interface", Package = "container/heap")]
        public interface IInterface
        {
            [GoMethod]
            [return: GoReturn("int")]
            long Len();

            [GoMethod]
            bool Less([GoParam("int")] long i, [GoParam("int")] long j);

            [GoMethod]
            void Swap([GoParam("int")] long i, [GoParam("int")] long j);

            [GoMethod]
            void Push(object? x);

            [GoMethod]
            [return: GoReturn("any")]
            object? Pop();
        }

        // heap.Init(h Interface)
        [GoFunc]
        public static void Init([GoParam("heap.Interface")] object? h) { }

        // heap.Push(h Interface, x any)
        [GoFunc]
        public static void Push([GoParam("heap.Interface")] object? h, object? x) { }

        // heap.Pop(h Interface) any
        [GoFunc]
        [return: GoReturn("any")]
        public static object? Pop([GoParam("heap.Interface")] object? h) => null;

        // heap.Remove(h Interface, i int) any
        [GoFunc]
        [return: GoReturn("any")]
        public static object? Remove([GoParam("heap.Interface")] object? h, [GoParam("int")] long i) => null;

        // heap.Fix(h Interface, i int)
        [GoFunc]
        public static void Fix([GoParam("heap.Interface")] object? h, [GoParam("int")] long i) { }
    }
}
