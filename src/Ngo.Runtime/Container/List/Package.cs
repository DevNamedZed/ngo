using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Container.List
{
    [GoPackage("container/list")]
    public static class Package
    {
        // list.New() *List
        [GoFunc]
        [return: GoReturn("*list.List")]
        public static GoList New() => new GoList();
    }

    [GoType("struct", Name = "Element", Package = "container/list")]
    public class GoElement
    {
        [GoField(Name = "Value")]
        public object? Value;

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? Next() => null;

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? Prev() => null;
    }

    [GoType("struct", Name = "List", Package = "container/list")]
    public class GoList
    {
        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? Back() => null;

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? Front() => null;

        [GoMethod]
        [return: GoReturn("*list.List")]
        public GoList Init() => this;

        [GoMethod]
        [return: GoReturn("int")]
        public long Len() => 0;

        [GoMethod]
        public void MoveToFront(GoElement? e) { }

        [GoMethod]
        public void MoveToBack(GoElement? e) { }

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? PushBack(object? v) => new GoElement { Value = v };

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? PushFront(object? v) => new GoElement { Value = v };

        [GoMethod]
        public object? Remove(GoElement? e) => e?.Value;

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? InsertAfter(object? v, GoElement? mark) => new GoElement { Value = v };

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? InsertBefore(object? v, GoElement? mark) => new GoElement { Value = v };

        [GoMethod]
        public void MoveBefore(GoElement? e, GoElement? mark) { }

        [GoMethod]
        public void MoveAfter(GoElement? e, GoElement? mark) { }

        [GoMethod]
        public void PushBackList(GoList? other) { }

        [GoMethod]
        public void PushFrontList(GoList? other) { }
    }
}
