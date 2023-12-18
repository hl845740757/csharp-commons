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

using System.Collections;

namespace Wjybxx.Commons.Collections;

/// <summary>
/// 基于多分块的双端队列
/// </summary>
public class MultiChunkDeque<T> : IDeque<T>
{
    private const int MinChunkSize = 16;
    private static readonly Stack<Chunk> EmptyChunkPool = new();

    private readonly int _chunkSize;
    private readonly int _poolSize;

    /** chunk序号 */
    private int _chunkSeq;
    /** 缓存块 */
    private readonly Stack<Chunk> _chunkPool;

    /** 队首所在块 */
    private Chunk? _headChunk;
    /** 队尾所在块 */
    private Chunk? _tailChunk;
    /** 元素数量缓存 */
    private int _count;
    /** 版本号 -- 增删元素时增加 */
    private int _version;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="chunkSize">单个块的大小</param>
    /// <param name="poolSize">Chunk池大小；0表示不缓存</param>
    public MultiChunkDeque(int chunkSize = 16, int poolSize = 4) {
        if (chunkSize < MinChunkSize) throw new ArgumentException("chunk is too small");
        if (poolSize < 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        _chunkSize = chunkSize;
        _poolSize = poolSize;
        _chunkPool = poolSize > 0 ? new Stack<Chunk>(poolSize) : EmptyChunkPool;
    }

    public bool IsReadOnly => false;
    public int Count => _count;

    #region get/contains

    public T PeekFirst() {
        if (_headChunk == null) {
            throw CollectionEmptyException();
        }
        return _headChunk.First;
    }

    public bool TryPeekFirst(out T item) {
        if (_headChunk == null) {
            item = default;
            return false;
        }
        item = _headChunk.First;
        return true;
    }

    public T PeekLast() {
        if (_tailChunk == null) {
            throw CollectionEmptyException();
        }
        return _tailChunk.Last;
    }

    public bool TryPeekLast(out T item) {
        if (_tailChunk == null) {
            item = default;
            return false;
        }
        item = _tailChunk.Last;
        return true;
    }

    public bool Contains(T item) {
        return GetChunk(item, out _) != null;
    }

    #endregion

    #region dequeue

    public void AddFirst(T item) {
        Insert(item, InsertionOrder.Head);
    }

    public void AddLast(T item) {
        Insert(item, InsertionOrder.Tail);
    }

    public bool TryAddFirst(T item) {
        Insert(item, InsertionOrder.Head);
        return true;
    }

    public bool TryAddLast(T item) {
        Insert(item, InsertionOrder.Tail);
        return true;
    }

    public T RemoveFirst() {
        if (_headChunk == null) {
            throw CollectionEmptyException();
        }
        T item = _headChunk.Last;
        Remove(_headChunk, item, _headChunk._firstIndex);
        return item;
    }

    public bool TryRemoveFirst(out T item) {
        if (_headChunk == null) {
            item = default;
            return false;
        }
        item = _headChunk.Last;
        Remove(_headChunk, item, _headChunk._firstIndex);
        return true;
    }

    public T RemoveLast() {
        if (_tailChunk == null) {
            throw CollectionEmptyException();
        }
        T item = _tailChunk.Last;
        Remove(_tailChunk, item, _tailChunk._lastIndex);
        return item;
    }

    public bool TryRemoveLast(out T item) {
        if (_tailChunk == null) {
            item = default;
            return false;
        }
        item = _tailChunk.Last;
        Remove(_tailChunk, item, _tailChunk._lastIndex);
        return true;
    }

    public bool Remove(T item) {
        Chunk chunk = GetChunk(item, out int index);
        if (chunk == null) return false;
        Remove(chunk, item, index);
        return true;
    }

    public void Clear() {
        if (_count > 0) {
            // 回收所有chunk
            for (Chunk chunk = _headChunk; chunk != null;) {
                Chunk nextChunk = chunk._next; // Release会清理引用，先暂存下来
                ReleaseChunk(chunk);
                chunk = nextChunk;
            }
            _headChunk = _tailChunk = null;
            _count = 0;
            _version++;
        }
    }

    public void AdjustCapacity(int expectedCount) {
        // TODO
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

    #endregion

    #region core

    private Chunk? GetChunk(T item, out int index) {
        index = -1;
        throw new NotImplementedException();
    }

    private void Insert(T item, InsertionOrder head) {
        throw new NotImplementedException();
    }

    private void Remove(Chunk chunk, T item, int index) {
        throw new NotImplementedException();
    }

    private void ReleaseChunk(Chunk chunk) {
        chunk.Reset();
        if (_chunkPool.Count < _poolSize) {
            _chunkPool.Push(chunk);
        }
    }

    private Chunk AllocChunk() {
        Chunk chunk;
        if (_chunkPool.Count > 0) {
            chunk = _chunkPool.Pop();
        }
        else {
            chunk = new Chunk(_chunkSize);
        }
        chunk._chunkIndex = _chunkSeq++; // version似乎是个好主意?
        return chunk;
    }

    #endregion

    #region util

    private static IEqualityComparer<T> Comparer => EqualityComparer<T>.Default;

    private static InvalidOperationException CollectionEmptyException() {
        return new InvalidOperationException("Collection is Empty");
    }

    #endregion

    /** 每一个Chunk是一个有界环形队列 */
    private class Chunk
    {
        internal T[] _elements;
        internal int _firstIndex;
        internal int _lastIndex;

        /** 用以识别chunk的有效性，当chunk被重用时，index会变化 */
        internal int _chunkIndex;
        internal Chunk? _prev;
        internal Chunk? _next;

        public Chunk(int length) {
            _elements = new T[length];
            _firstIndex = _lastIndex = -1;
        }

        public T First => _elements[_firstIndex];
        public T Last => _elements[_lastIndex];

        public int Count => _lastIndex == -1 ? 0 : (_lastIndex - _firstIndex + 1);

        public int IndexOf(T item) {
            IEqualityComparer<T> comparer = Comparer;
            T[] elements = _elements;
            for (int i = _firstIndex; i < _lastIndex; i++) {
                if (comparer.Equals(item, elements[i])) {
                    return i;
                }
            }
            return -1;
        }

        public void Reset() {
            Array.Fill(_elements, default);
            _firstIndex = _lastIndex = -1;
            _prev = null;
            _next = null;
        }
    }
}