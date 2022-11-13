namespace MyGame.Utils;

public class RingBuffer<T> : IEnumerable<T>
{
    private readonly T[] _buffer;
    private int _end;
    private int _start;

    public RingBuffer(int capacity)
    {
        Capacity = capacity;
        _buffer = new T[capacity];
    }

    public int Count { get; private set; }

    public int Capacity { get; }

    public bool IsEmpty => Count == 0;

    public T this[int index]
    {
        get
        {
            if (IsEmpty)
            {
                throw new IndexOutOfRangeException(string.Format("Cannot access index {0}. Buffer is empty", index));
            }

            if (index >= Count)
            {
                throw new IndexOutOfRangeException(string.Format("Cannot access index {0}. Buffer size is {1}", index, Count));
            }

            var actualIndex = InternalIndex(index);
            return _buffer[actualIndex];
        }
        set
        {
            if (IsEmpty)
            {
                throw new IndexOutOfRangeException(string.Format("Cannot access index {0}. Buffer is empty", index));
            }

            if (index >= Count)
            {
                throw new IndexOutOfRangeException(string.Format("Cannot access index {0}. Buffer size is {1}", index, Count));
            }

            var actualIndex = InternalIndex(index);
            _buffer[actualIndex] = value;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            var index = _start + i;
            if (index >= Capacity)
            {
                index -= Capacity;
            }

            yield return _buffer[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(T item)
    {
        if (Count == Capacity)
        {
            _buffer[_end] = item;
            _end = MathF.IncrementWithWrap(_end, Capacity);
            _start = _end;
        }
        else
        {
            _buffer[_end] = item;
            _end = MathF.IncrementWithWrap(_end, Capacity);
            Count++;
        }
    }

    private int InternalIndex(int index)
    {
        return _start + (index < Capacity - _start ? index : index - Capacity);
    }
}
