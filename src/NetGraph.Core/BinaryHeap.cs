namespace NetGraph.Core;

public readonly record struct HeapEntry<T>(float Priority, T Value);

/// <summary>Мин-куча на плоском массиве: без аллокаций узлов, дружелюбна к кэшу.</summary>
public sealed class BinaryHeap<T>
{
    private HeapEntry<T>[] _items;

    public int Count { get; private set; }

    public BinaryHeap(int capacity = 16)
        => _items = new HeapEntry<T>[Math.Max(1, capacity)];

    public void Push(float priority, T value)
    {
        if (Count == _items.Length)
            Array.Resize(ref _items, _items.Length * 2);

        int i = Count++;
        _items[i] = new HeapEntry<T>(priority, value);
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (_items[parent].Priority <= _items[i].Priority)
                break;
            (_items[parent], _items[i]) = (_items[i], _items[parent]);
            i = parent;
        }
    }

    public HeapEntry<T> Pop()
    {
        var top = _items[0];
        Count--;
        if (Count > 0)
        {
            _items[0] = _items[Count];
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1;
                int r = l + 1;
                int m = i;
                if (l < Count && _items[l].Priority < _items[m].Priority) m = l;
                if (r < Count && _items[r].Priority < _items[m].Priority) m = r;
                if (m == i) break;
                (_items[m], _items[i]) = (_items[i], _items[m]);
                i = m;
            }
        }
        _items[Count] = default!;
        return top;
    }

    public void Clear() => Count = 0;
}
