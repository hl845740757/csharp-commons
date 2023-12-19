#region LICENSE

// Copyright 2023 wjybxx(845740757@qq.com)
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System.Diagnostics;

namespace Wjybxx.Commons.Collections;

/// <summary>
/// 有界双端队列，固定大小不扩容，可以指定溢出策略
/// </summary>
public class BoundedArrayDeque<T> : IDeque<T>
{
    private readonly T[] _elements;
    private readonly DequeOverflowBehavior _overflowBehavior;

    /// <summary>
    /// 无元素的情况下head和tail都指向-1；有元素的情况下head和tail为对应的下标；
    /// 未环绕的情况下，元素数量可表示为<code>Count = tail - head + 1</code>
    /// </summary>
    private int _head;
    private int _tail;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="capacity">初始容量</param>
    /// <param name="overflowBehavior">溢出策略</param>
    /// <exception cref="ArgumentException"></exception>
    public BoundedArrayDeque(int capacity = 17,
                             DequeOverflowBehavior overflowBehavior = DequeOverflowBehavior.ThrowException) {
        if (capacity < 1) throw new ArgumentException(nameof(capacity));
        _elements = new T[capacity];
        _overflowBehavior = overflowBehavior;
        _head = _tail = -1;
    }

    public DequeOverflowBehavior OverflowBehavior => _overflowBehavior;

    public bool IsReadOnly => false;
    public int Count => _head == -1 ? 0 : Length(_tail, _head, _elements.Length);
    public bool IsEmpty => _head == -1;

    /// <summary>
    /// 读写特定索引下的元素
    /// </summary>
    /// <param name="index">[0, Count-1]</param>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public T this[int index] {
        get {
            T[] elements = _elements;
            int head = _head;
            if (index < 0 || head < 0 || index >= Length(_tail, head, elements.Length)) {
                throw new IndexOutOfRangeException($"count {Count}, index {index}");
            }
            return elements[Inc(head, index, elements.Length)];
        }
        set {
            T[] elements = _elements;
            int head = _head;
            if (index < 0 || head < 0 || index >= Length(_tail, head, elements.Length)) {
                throw new IndexOutOfRangeException($"count {Count}, index {index}");
            }
            elements[Inc(head, index, elements.Length)] = value;
        }
    }

    private static int Length(int tail, int head, int modulus) {
        Debug.Assert(head >= 0);
        if ((tail -= head) < 0) tail += modulus;
        return tail + 1;
    }

    private static int Inc(int i, int distance, int modulus) {
        if ((i += distance) - modulus >= 0) i -= modulus;
        return i;
    }

    private static int Inc(int i, int modulus) {
        if (++i >= modulus) i = 0;
        return i;
    }

    private static int Dec(int i, int modulus) {
        if (--i < 0) i = modulus - 1;
        return i;
    }

    #region sequence

    public T PeekFirst() {
        if (_head == -1) {
            throw CollectionUtil.CollectionEmptyException();
        }
        return _elements[_head];
    }

    public T PeekLast() {
        if (_head == -1) {
            throw CollectionUtil.CollectionEmptyException();
        }
        return _elements[_tail];
    }

    public bool TryPeekFirst(out T item) {
        if (_head == -1) {
            item = default;
            return false;
        }
        item = _elements[_head];
        return true;
    }

    public bool TryPeekLast(out T item) {
        if (_head == -1) {
            item = default;
            return false;
        }
        item = _elements[_tail];
        return true;
    }

    public void AddFirst(T item) {
        if (!TryAddFirst(item)) {
            throw CollectionUtil.CollectionFullException();
        }
    }

    public bool TryAddFirst(T item) {
        throw new NotImplementedException();
    }

    public void AddLast(T item) {
        if (!TryAddLast(item)) {
            throw CollectionUtil.CollectionFullException();
        }
    }

    public bool TryAddLast(T item) {
        throw new NotImplementedException();
    }

    public T RemoveFirst() {
        throw new NotImplementedException();
    }

    public bool TryRemoveFirst(out T item) {
        throw new NotImplementedException();
    }

    public T RemoveLast() {
        throw new NotImplementedException();
    }

    public bool TryRemoveLast(out T item) {
        throw new NotImplementedException();
    }

    public bool Contains(T item) {
        throw new NotImplementedException();
    }

    public bool Remove(T item) {
        throw new NotImplementedException();
    }

    public void Clear() {
        _tail = _head = -1;
        Array.Fill(_elements, default);
    }

    #endregion

    #region queue

    public void Enqueue(T item) {
        AddLast(item);
    }

    public bool TryEnqueue(T item) {
        return TryAddLast(item);
    }

    public T Dequeue() {
        return RemoveFirst();
    }

    public bool TryDequeue(out T item) {
        return TryRemoveFirst(out item);
    }

    public T PeekHead() {
        return PeekFirst();
    }

    public bool TryPeekHead(out T item) {
        return TryPeekFirst(out item);
    }

    #endregion

    #region stack

    public void Push(T item) {
        AddFirst(item);
    }

    public bool TryPush(T item) {
        return TryAddFirst(item);
    }

    public T Pop() {
        return RemoveFirst();
    }

    public bool TryPop(out T item) {
        return TryRemoveFirst(out item);
    }

    public T PeekTop() {
        return PeekFirst();
    }

    public bool TryPeekTop(out T item) {
        return TryPeekFirst(out item);
    }

    #endregion

    #region itr

    public IEnumerator<T> GetEnumerator() {
        throw new NotImplementedException();
    }

    public IEnumerator<T> GetReversedEnumerator() {
        throw new NotImplementedException();
    }

    public void CopyTo(T[] array, int arrayIndex, bool reversed = false) {
        throw new NotImplementedException();
    }

    public IDeque<T> Reversed() {
        return new ReversedDequeView<T>(this);
    }

    public void AdjustCapacity(int expectedCount) {
    }

    #endregion
}