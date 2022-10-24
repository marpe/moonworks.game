namespace MyGame.Utils;

public class RingBuffer<T> : IEnumerable<T>
{
	private T[] _buffer;
	private int _start;
	private int _end;
	private int _count;

	private readonly int _capacity;

	public int Count => _count;

	public int Capacity => _capacity;

	public bool IsEmpty => _count == 0;

	public RingBuffer(int capacity)
	{
		_capacity = capacity;
		_buffer = new T[capacity];
	}

	public void Add(T item)
	{
		if (_count == _capacity)
		{
			_buffer[_end] = item;
			_end = MathF.IncrementWithWrap(_end, _capacity);
			_start = _end;
		}
		else
		{
			_buffer[_end] = item;
			_end = MathF.IncrementWithWrap(_end, _capacity);
			_count++;
		}
	}

	public T this[int index]
	{
		get
		{
			if (IsEmpty)
			{
				throw new IndexOutOfRangeException(string.Format("Cannot access index {0}. Buffer is empty", index));
			}

			if (index >= _count)
			{
				throw new IndexOutOfRangeException(string.Format("Cannot access index {0}. Buffer size is {1}", index, _count));
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

			if (index >= _count)
			{
				throw new IndexOutOfRangeException(string.Format("Cannot access index {0}. Buffer size is {1}", index, _count));
			}

			var actualIndex = InternalIndex(index);
			_buffer[actualIndex] = value;
		}
	}

	private int InternalIndex(int index)
	{
		return _start + (index < (_capacity - _start) ? index : index - _capacity);
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (int i = 0; i < _count; i++)
		{
			var index = _start + i;
			if (index >= _capacity)
			{
				index -= _capacity;
			}
			yield return _buffer[index];
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
