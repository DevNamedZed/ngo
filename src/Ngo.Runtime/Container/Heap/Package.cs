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
        public static void Init([GoParam("heap.Interface")] object? h)
        {
            if (h is not IInterface heap)
            {
                return;
            }
            long n = heap.Len();
            for (long i = n / 2 - 1; i >= 0; i--)
            {
                Down(heap, i, n);
            }
        }

        // heap.Push(h Interface, x any)
        [GoFunc]
        public static void Push([GoParam("heap.Interface")] object? h, object? x)
        {
            if (h is not IInterface heap)
            {
                return;
            }
            heap.Push(x);
            Up(heap, heap.Len() - 1);
        }

        // heap.Pop(h Interface) any
        [GoFunc]
        [return: GoReturn("any")]
        public static object? Pop([GoParam("heap.Interface")] object? h)
        {
            if (h is not IInterface heap)
            {
                return null;
            }
            long n = heap.Len() - 1;
            heap.Swap(0, n);
            Down(heap, 0, n);
            return heap.Pop();
        }

        // heap.Remove(h Interface, i int) any
        [GoFunc]
        [return: GoReturn("any")]
        public static object? Remove([GoParam("heap.Interface")] object? h, [GoParam("int")] long i)
        {
            if (h is not IInterface heap)
            {
                return null;
            }
            long n = heap.Len() - 1;
            if (n != i)
            {
                heap.Swap(i, n);
                if (!Down(heap, i, n))
                {
                    Up(heap, i);
                }
            }
            return heap.Pop();
        }

        // heap.Fix(h Interface, i int)
        [GoFunc]
        public static void Fix([GoParam("heap.Interface")] object? h, [GoParam("int")] long i)
        {
            if (h is not IInterface heap)
            {
                return;
            }
            if (!Down(heap, i, heap.Len()))
            {
                Up(heap, i);
            }
        }

        private static void Up(IInterface h, long j)
        {
            while (true)
            {
                long i = (j - 1) / 2; // parent
                if (i == j || !h.Less(j, i))
                {
                    break;
                }
                h.Swap(i, j);
                j = i;
            }
        }

        private static bool Down(IInterface h, long i0, long n)
        {
            long i = i0;
            while (true)
            {
                long j1 = 2 * i + 1; // left child
                if (j1 >= n || j1 < 0)
                {
                    break;
                }
                long j = j1;
                long j2 = j1 + 1; // right child
                if (j2 < n && h.Less(j2, j1))
                {
                    j = j2;
                }
                if (!h.Less(j, i))
                {
                    break;
                }
                h.Swap(i, j);
                i = j;
            }
            return i > i0;
        }
    }
}
