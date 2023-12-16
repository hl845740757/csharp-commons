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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Wjybxx.Commons.Collections;

/// <summary>
/// 保持插入序的Set
/// 1.由<see cref="LinkedDictionary{TKey,TValue}"/>修改而来，保留起特性。
/// 2.使用拷贝而不是封装的方式，以减少使用开销。
/// 
/// </summary>
/// <typeparam name="TKey"></typeparam>
[Serializable]
public class LinkedHashSet<TKey> : ISequencedSet<TKey>, ISerializable
{
    /** len = 2^n + 1，额外的槽用于存储nullKey；总是延迟分配空间，以减少创建空实例的开销 */
    private Node?[]? _table; // 这个NullableReference有时真的很烦
    private Node? _head;
    private Node? _tail;

    /** 有效元素数量 */
    private int _count;
    /** 版本号 -- 发生结构性变化的时候增加，即增加和删除元素的时候；替换Key的Value不增加版本号 */
    private int _version;

    /** 当前计算下标使用的掩码，不依赖数组长度；相反，我们可以通过mask获得数组的真实长度 */
    private int _mask;
    /** 负载因子 */
    private float _loadFactor;
    /** 用于代替key自身的equals和hashcode计算；这一点C#的设计做的要好些 */
    private IEqualityComparer<TKey> _keyComparer;

    public LinkedHashSet()
        : this(0, HashCommon.DefaultLoadFactor) {
    }

    public LinkedHashSet(ICollection<TKey> src)
        : this(src.Count, HashCommon.DefaultLoadFactor) {
        AddRange(src);
    }

    public LinkedHashSet(IEqualityComparer<TKey> comparer)
        : this(0, HashCommon.DefaultLoadFactor, comparer) {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="expectedCount">期望存储的元素个数，而不是直接的容量</param>
    /// <param name="loadFactor">有效负载因子</param>
    /// <param name="keyComparer">可用于避免Key比较时装箱</param>
    public LinkedHashSet(int expectedCount, float loadFactor = 0.75f,
                         IEqualityComparer<TKey>? keyComparer = null) {
        if (expectedCount < 0) throw new ArgumentException("The expected number of elements must be nonnegative");
        HashCommon.CheckLoadFactor(loadFactor);
        _loadFactor = loadFactor;
        _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;

        if (expectedCount == 0) {
            expectedCount = HashCommon.DefaultInitialSize;
        }
        _mask = HashCommon.ArraySize(expectedCount, loadFactor) - 1;
    }

    public int Count => _count;
    public bool IsReadOnly => false;

    #region peek

    public TKey First {
        get {
            if (_head == null) throw CollectionEmptyException();
            return _head._key;
        }
    }

    public bool PeekFirst(out TKey key) {
        if (_head == null) {
            key = default;
            return false;
        }
        key = _head._key;
        return true;
    }

    public TKey Last {
        get {
            if (_tail == null) throw CollectionEmptyException();
            return _tail._key;
        }
    }

    public bool PeekLast(out TKey key) {
        if (_tail == null) {
            key = default;
            return false;
        }
        key = _tail._key;
        return true;
    }

    #endregion

    #region contains

    public bool Contains(TKey key) {
        return GetNode(key) != null;
    }

    #endregion

    #region add

    public bool Add(TKey key) {
        return TryPut(key, PutBehavior.None);
    }

    public bool AddFirst(TKey key) {
        return TryPut(key, PutBehavior.MoveToFirst);
    }

    public bool AddLast(TKey key) {
        return TryPut(key, PutBehavior.MoveToLast);
    }

    public bool AddFirstIfAbsent(TKey key) {
        return TryInsert(key, InsertionOrder.Head, InsertionBehavior.None);
    }

    public bool AddLastIfAbsent(TKey key) {
        return TryInsert(key, InsertionOrder.Tail, InsertionBehavior.None);
    }

    public bool AddRange(IEnumerable<TKey> collection) {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        if (collection is ICollection<TKey> c) {
            if (_loadFactor <= 0.5f) {
                EnsureCapacity(c.Count); // 负载小于0.5，数组的长度将大于等于count的2倍，就能放下所有元素
            }
            else {
                TryCapacity(_count + c.Count);
            }
        }
        bool r = false;
        foreach (TKey key in collection) {
            r |= TryPut(key, PutBehavior.None);
        }
        return r;
    }

    #endregion

    #region remove

    public bool Remove(TKey key) {
        var node = GetNode(key);
        if (node == null) {
            return false;
        }
        RemoveNode(node);
        return true;
    }

    public TKey RemoveFirst() {
        if (TryRemoveFirst(out TKey r)) {
            return r;
        }
        throw CollectionEmptyException();
    }

    public bool TryRemoveFirst(out TKey key) {
        Node oldHead = _head;
        if (oldHead == null) {
            key = default;
            return false;
        }

        key = oldHead._key;
        _count--;
        _version++;
        FixPointers(oldHead);
        ShiftKeys(oldHead._index);
        oldHead.AfterRemoved();
        return true;
    }

    public TKey RemoveLast() {
        if (TryRemoveLast(out TKey r)) {
            return r;
        }
        throw CollectionEmptyException();
    }

    public bool TryRemoveLast(out TKey key) {
        Node oldTail = _tail;
        if (oldTail == null) {
            key = default;
            return false;
        }
        key = oldTail._key;

        _count--;
        _version++;
        FixPointers(oldTail);
        ShiftKeys(oldTail._index);
        oldTail.AfterRemoved();
        return true;
    }

    public void Clear() {
        int count = _count;
        if (count > 0) {
            _count = 0;
            _version++;
            _head = _tail = null;
            Array.Clear(_table!);
        }
    }

    /** 用于子类更新版本号 */
    protected void IncVersion() => _version++;

    #endregion

    #region sp

    /// <summary>
    /// 查询指定键的后一个键
    /// (未重命名是故意的，Next很容易误用)
    /// </summary>
    /// <param name="key">当前键</param>
    /// <param name="next">接收下一个键</param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">如果当前键不存在</exception>
    public bool NextKey(TKey key, out TKey next) {
        var node = GetNode(key);
        if (node == null) {
            throw KeyNotFoundException(key);
        }
        if (node._next != null) {
            next = node._next._key;
            return true;
        }
        next = default;
        return false;
    }

    /// <summary>
    /// 查询指定键的前一个键
    /// </summary>
    /// <param name="key">当前键</param>
    /// <param name="prev">接收前一个键</param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">如果当前键不存在</exception>
    public bool PrevKey(TKey key, out TKey prev) {
        var node = GetNode(key);
        if (node == null) {
            throw KeyNotFoundException(key);
        }
        if (node._prev != null) {
            prev = node._prev._key;
            return true;
        }
        prev = default;
        return false;
    }

    public void AdjustCapacity(int expectedCount) {
        if (expectedCount < _count) {
            throw new ArgumentException($"expectedCount:{expectedCount} < count {_count}");
        }
        int arraySize = HashCommon.ArraySize(expectedCount, _loadFactor);
        if (arraySize <= HashCommon.DefaultInitialSize) {
            return;
        }
        int curArraySize = _mask + 1;
        if (arraySize == curArraySize) {
            return;
        }
        if (arraySize < curArraySize) {
            if (_count > HashCommon.MaxFill(arraySize, _loadFactor)) {
                return; // 避免收缩后空间不足
            }
            if (Math.Abs(arraySize - curArraySize) <= HashCommon.DefaultInitialSize) {
                return; // 避免不必要的收缩
            }
        }
        if (_table == null) {
            _mask = arraySize - 1;
        }
        else {
            Rehash(arraySize);
        }
    }

    #endregion

    #region copyto

    public void CopyTo(TKey[] array, int arrayIndex) {
        CopyTo(array, arrayIndex, false);
    }

    public void CopyTo(TKey[] array, int arrayIndex, bool reversed) {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Array is too small");

        if (reversed) {
            for (Node e = _tail; e != null; e = e._prev) {
                array[arrayIndex++] = e._key;
            }
        }
        else {
            for (Node e = _head; e != null; e = e._next) {
                array[arrayIndex++] = e._key;
            }
        }
    }

    #endregion

    #region itr

    public ISequencedSet<TKey> Reversed() {
        return new ReversedSequenceSetView<TKey>(this);
    }

    public IEnumerator<TKey> GetEnumerator() {
        return new SetIterator(this, false);
    }

    public IEnumerator<TKey> GetReversedEnumerator() {
        return new SetIterator(this, true);
    }

    #endregion

    #region core

    /// <summary>
    /// 如果key存在，则返回对应的下标(大于等于0)；
    /// 如果key不存在，则返回其hash应该存储的下标的负值再减1，以识别0 -- 或者说 下标 +1 再取相反数。
    /// 该方法只有增删方法元素方法可调用，会导致初始化空间
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hash">key的hash值</param>
    /// <returns></returns>
    private int Find(TKey key, int hash) {
        Node[] table = _table;
        if (table == null) {
            table = _table = new Node[_mask + 2];
        }
        if (key == null) {
            Node nullNode = table[_mask + 1];
            return nullNode == null ? -(_mask + 2) : (_mask + 1);
        }

        IEqualityComparer<TKey> keyComparer = _keyComparer;
        int mask = _mask;
        // 先测试无冲突位置
        int pos = mask & hash;
        Node node = table[pos];
        if (node == null) return -(pos + 1);
        if (node._hash == hash && keyComparer.Equals(node._key, key)) {
            return pos;
        }
        // 线性探测
        // 注意：为了利用空间，线性探测需要在越界时绕回到数组首部(mask取余绕回)；'i'就是探测次数
        // 由于数组满时一定会触发扩容，可保证这里一定有一个槽为null；如果循环一圈失败，上次扩容失败被捕获？
        for (int i = 0; i < mask; i++) {
            pos = (pos + 1) & mask;
            node = table[pos];
            if (node == null) return -(pos + 1);
            if (node._hash == hash && keyComparer.Equals(node._key, key)) {
                return pos;
            }
        }
        throw new InvalidOperationException("state error");
    }

    /** 该接口仅适用于查询方法使用 */
    private Node? GetNode(TKey key) {
        Node[] table = _table;
        if (table == null || _count == 0) {
            return null;
        }
        if (key == null) {
            return table[_mask + 1];
        }
        IEqualityComparer<TKey> keyComparer = _keyComparer;
        int mask = _mask;
        // 先测试无冲突位置
        int hash = KeyHash(key, keyComparer);
        int pos = mask & hash;
        Node node = table[pos];
        if (node == null) return null;
        if (node._hash == hash && keyComparer.Equals(node._key, key)) {
            return node;
        }
        for (int i = 0; i < mask; i++) {
            pos = (pos + 1) & mask;
            node = table[pos];
            if (node == null) return null;
            if (node._hash == hash && keyComparer.Equals(node._key, key)) {
                return node;
            }
        }
        throw new InvalidOperationException("state error");
    }

    /** 如果是insert则返回true */
    private bool TryInsert(TKey key, InsertionOrder order, InsertionBehavior behavior) {
        int hash = KeyHash(key, _keyComparer);
        int pos = Find(key, hash);
        if (pos >= 0) {
            if (behavior == InsertionBehavior.ThrowOnExisting) {
                throw new InvalidOperationException("AddingDuplicateWithKey: " + key);
            }
            return false;
        }

        pos = -pos - 1;
        Insert(pos, hash, key, order);
        return true;
    }

    /** 如果是insert则返回true */
    private bool TryPut(TKey key, PutBehavior behavior) {
        int hash = KeyHash(key, _keyComparer);
        int pos = Find(key, hash);
        if (pos >= 0) {
            Node existNode = _table![pos]!;
            if (behavior == PutBehavior.MoveToLast) {
                MoveToLast(existNode);
            }
            else if (behavior == PutBehavior.MoveToFirst) {
                MoveToFirst(existNode);
            }
            return false;
        }

        pos = -pos - 1;
        switch (behavior) {
            case PutBehavior.MoveToFirst:
                Insert(pos, hash, key, InsertionOrder.Head);
                break;
            case PutBehavior.MoveToLast:
                Insert(pos, hash, key, InsertionOrder.Tail);
                break;
            case PutBehavior.None:
            default:
                Insert(pos, hash, key, InsertionOrder.Default);
                break;
        }
        return true;
    }

    private void Insert(int pos, int hash, TKey key, InsertionOrder order) {
        Node node = new Node(hash, key, pos);
        if (_count == 0) {
            _head = _tail = node;
        }
        else if (order == InsertionOrder.Head) {
            node._next = _head;
            _head!._prev = node;
            _head = node;
        }
        else {
            node._prev = _tail;
            _tail!._next = node;
            _tail = node;
        }
        _count++;
        _version++;
        _table![pos] = node;

        // 不再缓存maxFill，因为只有插入元素的时候计算，不会太频繁
        int maxFill = HashCommon.MaxFill(_mask + 1, _loadFactor);
        if (_count >= maxFill) {
            Rehash(HashCommon.ArraySize(_count + 1, _loadFactor));
        }
    }

    private void Rehash(int newSize) {
        Debug.Assert(newSize >= _count);
        Node[] oldTable = _table!;
        Node[] newTable = new Node[newSize + 1];

        int mask = newSize - 1;
        int pos;
        // 遍历旧table数组会更快，数据更连续
        int remain = _count;
        for (var i = 0; i < oldTable.Length; i++) {
            var node = oldTable[i];
            if (node == null) {
                continue;
            }
            if (node._key == null) {
                pos = mask + 1;
            }
            else {
                pos = node._hash & mask;
                while (newTable[pos] != null) {
                    pos = (pos + 1) & mask;
                }
            }
            newTable[pos] = node;
            node._index = pos;
            if (--remain == 0) {
                break;
            }
        }
        this._table = newTable;
        this._mask = mask;
    }

    private void EnsureCapacity(int capacity) {
        int arraySize = HashCommon.ArraySize(capacity, _loadFactor);
        if (arraySize > _mask + 1) {
            Rehash(arraySize);
        }
    }

    private void TryCapacity(int capacity) {
        int arraySize = HashCommon.TryArraySize(capacity, _loadFactor);
        if (arraySize > _mask + 1) {
            Rehash(arraySize);
        }
    }

    /** 删除指定节点 -- 该方法为通用情况；需要处理Head和Tail的情况 */
    private void RemoveNode(Node node) {
        _count--;
        _version++;
        FixPointers(node);
        ShiftKeys(node._index);
        node.AfterRemoved(); // 可以考虑自动收缩空间
    }

    /// <summary>
    /// 删除pos位置的元素，将后续相同hash值的元素前移；
    /// 在调用该方法前，应当先调用 FixPointers 修正被删除节点的索引信息。
    /// </summary>
    /// <param name="pos"></param>
    private void ShiftKeys(int pos) {
        if (pos == _mask + 1) { // nullKey
            return;
        }
        Node[] table = _table!;
        int mask = _mask;

        int last, slot;
        Node curr;
        // 需要双层for循环；因为当前元素移动后，可能引发其它hash值的元素移动
        while (true) {
            last = pos;
            pos = (pos + 1) & mask; // + 1 可能绕回到首部
            while (true) {
                curr = table[pos];
                if (curr == null) {
                    table[last] = null;
                    return;
                }
                // [slot   last .... pos   slot] slot是应该属于的位置，pos是实际的位置，slot在连续区间外则应该移动
                slot = curr._hash & mask;
                if (last <= pos ? (last >= slot || slot > pos) : (last >= slot && slot > pos)) break;
                pos = (pos + 1) & mask;
            }
            table[last] = curr;
            curr._index = last;
        }
    }

    /// <summary>
    /// 解除Node的引用
    /// 在调用该方法前需要先更新count和version，在Node真正删除后才可清理Node数据
    /// </summary>
    /// <param name="node">要解除引用的节点</param>
    private void FixPointers(Node node) {
        if (_count == 0) {
            _head = _tail = null;
        }
        else if (node == _head) {
            _head = node._next!;
            _head._prev = null;
        }
        else if (node == _tail) {
            _tail = node._prev!;
            _tail._next = null;
        }
        else {
            // 删除的是中间元素
            var prev = node._prev!;
            var next = node._next!;
            prev._next = next;
            next._prev = prev;
        }
    }

    private void MoveToFirst(Node node) {
        if (_count == 1 || node == _head) {
            return;
        }
        if (node == _tail) {
            _tail = node._prev!;
            _tail._next = null;
        }
        else {
            var prev = node._prev!;
            var next = node._next!;
            prev._next = next;
            next._prev = prev;
        }
        node._next = _head;
        _head!._prev = node;
        _head = node;
    }

    private void MoveToLast(Node node) {
        if (_count == 1 || node == _tail) {
            return;
        }
        if (node == _head) {
            _head = node._next!;
            _head._prev = null;
        }
        else {
            var prev = node._prev!;
            var next = node._next!;
            prev._next = next;
            next._prev = prev;
        }
        node._prev = _tail;
        _tail!._next = node;
        _tail = node;
    }

    #endregion

    #region util

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int KeyHash(TKey? key, IEqualityComparer<TKey> keyComparer) {
        return key == null ? 0 : HashCommon.Mix(keyComparer.GetHashCode(key));
    }

    private static InvalidOperationException CollectionEmptyException() {
        return new InvalidOperationException("Dictionary is Empty");
    }

    private static KeyNotFoundException KeyNotFoundException(TKey key) {
        return new KeyNotFoundException(key == null ? "null" : key.ToString());
    }

    #endregion

    #region view

    private class SetIterator : IRemovableIterator<TKey>
    {
        private readonly LinkedHashSet<TKey> _hashSet;
        private readonly bool _reversed;
        private int _version;

        private Node? _currNode;
        private Node? _nextNode;
        private TKey _current;

        internal SetIterator(LinkedHashSet<TKey> hashSet, bool reversed) {
            _hashSet = hashSet;
            _reversed = reversed;
            _version = hashSet._version;

            _nextNode = _reversed ? _hashSet._tail : _hashSet._head;
            _current = default;
        }

        public bool MoveNext() {
            if (_version != _hashSet._version) {
                throw new InvalidOperationException("EnumFailedVersion");
            }
            if (_nextNode == null) {
                _current = default;
                return false;
            }
            Node node = _currNode = _nextNode;
            _nextNode = _reversed ? node._prev : node._next;
            // 其实这期间node的value可能变化，安全的话应该每次创建新的Pair，但c#系统库没这么干
            _current = node._key;
            return true;
        }

        public bool Remove() {
            if (_version != _hashSet._version) {
                throw new InvalidOperationException("EnumFailedVersion");
            }
            if (_currNode == null || _currNode._index < 0) {
                throw new InvalidOperationException("AlreadyRemoved");
            }
            _hashSet.RemoveNode(_currNode);
            _currNode = null;
            _version = _hashSet._version;
            return true;
        }

        public void Reset() {
            if (_version != _hashSet._version) {
                throw new InvalidOperationException("EnumFailedVersion");
            }
            _currNode = null;
            _nextNode = _reversed ? _hashSet._tail : _hashSet._head;
            _current = default;
        }

        public TKey Current => _current;

        object IEnumerator.Current => Current;

        public void Dispose() {
        }
    }

    private class Node
    {
        /** 由于Key的hash使用频率极高，缓存以减少求值开销 */
        internal readonly int _hash;
        internal readonly TKey? _key;
        /** 由于使用线性探测法，删除的元素不一定直接位于hash槽上，需要记录，以便快速删除；-1表示已删除 */
        internal int _index;

        internal Node? _prev;
        internal Node? _next;

        public Node(int hash, TKey? key, int index) {
            _hash = hash;
            _key = key;
            _index = index;
        }

        public void AfterRemoved() {
            _index = -1;
            _prev = null;
            _next = null;
        }

        public override int GetHashCode() {
            return _hash; // 不使用value计算hash，因为value可能在中途变更
        }

        public override string ToString() {
            return $"{nameof(_key)}: {_key}";
        }
    }

    #endregion

    #region seril

    private const string Names_Mask = "Mask";
    private const string Names_LoadFactor = "LoadFactor";
    private const string Names_Comparer = "Comparer";
    private const string Names_Keys = "Keys";

    public virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
        if (info == null) throw new ArgumentNullException(nameof(info));

        info.AddValue(Names_Mask, _mask);
        info.AddValue(Names_LoadFactor, _loadFactor);
        info.AddValue(Names_Comparer, _keyComparer, typeof(IEqualityComparer<TKey>));
        if (_table != null && _count > 0) { // 有数据才序列化
            var array = new TKey[Count];
            CopyTo(array, 0, false);
            info.AddValue(Names_Keys, array, typeof(TKey[]));
        }
    }

    protected LinkedHashSet(SerializationInfo info, StreamingContext context) {
        _mask = info.GetInt32(Names_Mask);
        _loadFactor = info.GetSingle(Names_LoadFactor);
        _keyComparer = (IEqualityComparer<TKey>)info.GetValue(Names_Comparer, typeof(IEqualityComparer<TKey>)) ?? EqualityComparer<TKey>.Default;

        HashCommon.CheckLoadFactor(_loadFactor);
        if (_mask + 1 != MathCommon.NextPowerOfTwo(_mask)) {
            throw new Exception("invalid serial data, _mask: " + _mask);
        }

        TKey[] keys = (TKey[])info.GetValue(Names_Keys, typeof(TKey[]));
        if (keys != null && keys.Length > 0) {
            BuildTable(keys);
        }
    }

    private void BuildTable(TKey[] keyArray) {
        // 构建Node链
        IEqualityComparer<TKey> keyComparer = _keyComparer;
        Node head;
        {
            TKey key = keyArray[0];
            int hash = KeyHash(key, keyComparer);
            head = new Node(hash, key, -1);
        }
        Node tail = head;
        for (var i = 1; i < keyArray.Length; i++) {
            TKey key = keyArray[0];
            int hash = KeyHash(key, keyComparer);
            Node next = new Node(hash, key, -1);
            //
            tail._next = next;
            next._prev = tail;
            tail = next;
        }
        _head = head;
        _tail = tail;

        // 散列到数组 -- 走正常的Find方法更安全些
        _table = new Node[_mask + 2];
        _count = keyArray.Length;
        for (Node node = _head; node != null; node = node._next) {
            int pos = Find(node._key, node._hash);
            if (pos >= 0) {
                throw new SerializationException("invalid serial data");
            }
            pos = -pos - 1;
            _table[pos] = node;
        }
    }

    #endregion
}