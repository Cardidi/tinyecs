namespace TinyECS.Core.Vendor
{
    public readonly struct SortedObject<T> : IComparable<SortedObject<T>>, IEquatable<SortedObject<T>>
    {
        public static int IndexOfElement(IReadOnlyList<SortedObject<T>> c, in T ele)
        {
            for (var i = 0; i < c.Count; i++)
            {
                if (c[i].Element.Equals(ele)) return i;
            }

            return -1;
        }
        
        public readonly int Order;

        public readonly T Element;

        public SortedObject(int order, T element)
        {
            Order = order;
            Element = element;
        }

        public int CompareTo(SortedObject<T> other)
        {
            return Order.CompareTo(other.Order);
        }

        public bool Equals(SortedObject<T> other)
        {
            return Order == other.Order && EqualityComparer<T>.Default.Equals(Element, other.Element);
        }

        public override bool Equals(object obj)
        {
            return obj is SortedObject<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Order * 397) ^ EqualityComparer<T>.Default.GetHashCode(Element);
            }
        }
    }
}