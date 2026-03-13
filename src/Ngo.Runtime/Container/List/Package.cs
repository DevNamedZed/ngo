using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Container.List
{
    [GoPackage("container/list")]
    public static class Package
    {
        // list.New() *List
        [GoFunc]
        [return: GoReturn("*list.List")]
        public static GoList New()
        {
            var l = new GoList();
            l.Init();
            return l;
        }
    }

    [GoType("struct", Name = "Element", Package = "container/list")]
    public class GoElement
    {
        [GoField(Name = "Value")]
        public object? Value;

        // Internal linked list pointers — not exported in Go
        internal GoElement? _next;
        internal GoElement? _prev;
        internal GoList? _list;

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? Next()
        {
            if (_list != null && _next != _list._root)
            {
                return _next;
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? Prev()
        {
            if (_list != null && _prev != _list._root)
            {
                return _prev;
            }
            return null;
        }
    }

    [GoType("struct", Name = "List", Package = "container/list")]
    public class GoList
    {
        // Sentinel element — root.next is front, root.prev is back
        internal GoElement _root;
        private long _len;

        public GoList()
        {
            _root = new GoElement();
            _root._next = _root;
            _root._prev = _root;
            _len = 0;
        }

        [GoMethod]
        [return: GoReturn("*list.List")]
        public GoList Init()
        {
            _root._next = _root;
            _root._prev = _root;
            _len = 0;
            return this;
        }

        [GoMethod]
        [return: GoReturn("int")]
        public long Len()
        {
            return _len;
        }

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? Front()
        {
            if (_len == 0)
            {
                return null;
            }
            return _root._next;
        }

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? Back()
        {
            if (_len == 0)
            {
                return null;
            }
            return _root._prev;
        }

        private GoElement InsertAfter(GoElement e, GoElement at)
        {
            e._prev = at;
            e._next = at._next;
            e._prev!._next = e;
            e._next!._prev = e;
            e._list = this;
            _len++;
            return e;
        }

        private void RemoveElement(GoElement e)
        {
            e._prev!._next = e._next;
            e._next!._prev = e._prev;
            e._next = null;
            e._prev = null;
            e._list = null;
            _len--;
        }

        private void Move(GoElement e, GoElement at)
        {
            if (e == at)
            {
                return;
            }
            e._prev!._next = e._next;
            e._next!._prev = e._prev;
            e._prev = at;
            e._next = at._next;
            e._prev._next = e;
            e._next!._prev = e;
        }

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? PushFront(object? v)
        {
            var e = new GoElement { Value = v };
            return InsertAfter(e, _root);
        }

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? PushBack(object? v)
        {
            var e = new GoElement { Value = v };
            return InsertAfter(e, _root._prev!);
        }

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? InsertBefore(object? v, GoElement? mark)
        {
            if (mark == null || mark._list != this)
            {
                return null;
            }
            var e = new GoElement { Value = v };
            return InsertAfter(e, mark._prev!);
        }

        [GoMethod]
        [return: GoReturn("*list.Element")]
        public GoElement? InsertAfter(object? v, GoElement? mark)
        {
            if (mark == null || mark._list != this)
            {
                return null;
            }
            var e = new GoElement { Value = v };
            return InsertAfter(e, mark);
        }

        [GoMethod]
        public object? Remove(GoElement? e)
        {
            if (e == null || e._list != this)
            {
                return null;
            }
            RemoveElement(e);
            return e.Value;
        }

        [GoMethod]
        public void MoveToFront(GoElement? e)
        {
            if (e == null || e._list != this || _root._next == e)
            {
                return;
            }
            Move(e, _root);
        }

        [GoMethod]
        public void MoveToBack(GoElement? e)
        {
            if (e == null || e._list != this || _root._prev == e)
            {
                return;
            }
            Move(e, _root._prev!);
        }

        [GoMethod]
        public void MoveBefore(GoElement? e, GoElement? mark)
        {
            if (e == null || mark == null || e._list != this || mark._list != this || e == mark)
            {
                return;
            }
            Move(e, mark._prev!);
        }

        [GoMethod]
        public void MoveAfter(GoElement? e, GoElement? mark)
        {
            if (e == null || mark == null || e._list != this || mark._list != this || e == mark)
            {
                return;
            }
            Move(e, mark);
        }

        [GoMethod]
        public void PushBackList(GoList? other)
        {
            if (other == null)
            {
                return;
            }
            long count = other.Len();
            var next = other.Front();
            for (long i = 0; i < count; i++)
            {
                if (next == null)
                {
                    break;
                }
                var val = next.Value;
                next = next.Next();
                PushBack(val);
            }
        }

        [GoMethod]
        public void PushFrontList(GoList? other)
        {
            if (other == null)
            {
                return;
            }
            long count = other.Len();
            var prev = other.Back();
            for (long i = 0; i < count; i++)
            {
                if (prev == null)
                {
                    break;
                }
                var val = prev.Value;
                prev = prev.Prev();
                PushFront(val);
            }
        }
    }
}
