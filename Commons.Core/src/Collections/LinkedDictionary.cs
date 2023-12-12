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
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wjybxx.Commons.Collections;

/// <summary>
/// 保持插入序的字典
/// 1.使用简单的线性探测法解决Hash冲突，因此在数据量较大的情况下查询性能会降低 -- 不想写得太复杂。
/// 2.算法参考自FastUtil的LinkedOpenHashMap
/// 3.非线程安全。
///
/// 吐槽：C#的基础库里居然没有保持插入序的高性能字典，这对于编写底层工具的开发者来说太不方便了。
/// 
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class LinkedDictionary<TKey, TValue> : ISequencedDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue> where TKey : notnull
{
    // C#的泛型是独立的类，因此缓存是独立的
    private static readonly bool KeyIsValueType = typeof(TKey).IsValueType;
    private static readonly bool ValueIsValueType = typeof(TValue).IsValueType;

    /** 总是延迟分配空间，以减少创建Dictionary的开销 */
    private Node?[]? _table; // 这个NullableReference有时真的很烦
    private Node? _head;
    private Node? _tail;

    /** 有效元素数量 */
    private int _count;
    /** 版本号 -- 发生结构性变化的时候增加，即增加和删除元素的时候；替换Key的Value不增加版本号 */
    private int _version;

    /** 计算下标使用的掩码，不依赖数组长度，避免未来调整时破坏太大；相反，我们可以通过mask获得数组的真实长度 */
    private int _mask;
    /** 负载因子 */
    private float _loadFactor;
    /** count触发扩容的边界值 */
    private int _maxFill;

    /** 用于代替key自身的equals和hashcode计算；这一点C#的设计做的要好些 */
    private IEqualityComparer<TKey> _keyComparer;
    private KeyCollection? _keys;
    private ValueCollection? _values;

    public LinkedDictionary()
        : this(0, HashCommon.DefaultLoadFactor) {
    }

    public LinkedDictionary(IEqualityComparer<TKey> comparer)
        : this(0, HashCommon.DefaultLoadFactor, comparer) {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="expectedCount">期望存储的元素个数，而不是直接的容量</param>
    /// <param name="loadFactor">有效负载因子</param>
    /// <param name="keyComparer">可用于避免Key比较时装箱</param>
    public LinkedDictionary(int expectedCount, float loadFactor = 0.75f,
                            IEqualityComparer<TKey>? keyComparer = null) {
        if (expectedCount < 0) throw new ArgumentException("The expected number of elements must be nonnegative");
        HashCommon.CheckLoadFactor(loadFactor);
        _loadFactor = loadFactor;
        _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;

        if (expectedCount == 0) {
            expectedCount = HashCommon.DefaultInitialSize;
        }
        int arraySize = HashCommon.ArraySize(expectedCount, loadFactor);
        _mask = arraySize - 1;
        _maxFill = HashCommon.MaxFill(arraySize, loadFactor);
    }


    public int Count => _count;
    public bool IsReadOnly => false;

    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    bool IDictionary.IsFixedSize => false;
    bool IDictionary.IsReadOnly => false;

    public IGenericCollection<TKey> Keys => _keys ?? new KeyCollection(this);
    public IGenericCollection<TValue> Values => _values ?? new ValueCollection(this);
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => _keys ?? new KeyCollection(this);
    ICollection<TValue> IDictionary<TKey, TValue>.Values => _values ?? new ValueCollection(this);
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _keys ?? new KeyCollection(this);
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _values ?? new ValueCollection(this);

    public TValue this[TKey key] {
        get {
            Node? node = GetNode(key);
            return node == null ? default : node._value;
        }
        set => TryInsert(key, value, InsertionOrder.Default, InsertionBehavior.OverwriteExisting);
    }

    private IEqualityComparer<TValue> ValComparer => EqualityComparer<TValue>.Default;

    #region peek

    public KeyValuePair<TKey, TValue> FirstPair {
        get {
            if (_head == null) throw DictionaryEmptyException();
            return _head.AsPair();
        }
    }

    public TKey FirstKey {
        get {
            if (_head == null) throw DictionaryEmptyException();
            return _head._key;
        }
    }

    public TValue FirstValue {
        get {
            if (_head == null) throw DictionaryEmptyException();
            return _head._value;
        }
    }

    public KeyValuePair<TKey, TValue> LastPair {
        get {
            if (_tail == null) throw DictionaryEmptyException();
            return _tail.AsPair();
        }
    }

    public TKey LastKey {
        get {
            if (_tail == null) throw DictionaryEmptyException();
            return _tail._key;
        }
    }

    public TValue LastValue {
        get {
            if (_tail == null) throw DictionaryEmptyException();
            return _tail._value;
        }
    }

    private static InvalidOperationException DictionaryEmptyException() {
        return new InvalidOperationException("Dictionary is Empty");
    }

    #endregion

    #region get

    public bool ContainsKey(TKey key) {
        return GetNode(key) != null;
    }

    public bool ContainsValue(TValue value) {
        if (value == null) {
            for (Node e = _head; e != null; e = e._next) {
                if (e._value == null) {
                    return true;
                }
            }
            return false;
        }
        else {
            IEqualityComparer<TValue>? valComparer = ValComparer;
            for (Node e = _head; e != null; e = e._next) {
                if (valComparer.Equals(e._value, value)) {
                    return true;
                }
            }
            return false;
        }
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) {
        Node node = GetNode(item.Key);
        return node != null && ValComparer.Equals(node._value, item.Value);
    }

    public bool TryGetValue(TKey key, out TValue value) {
        var node = GetNode(key);
        if (node == null) {
            value = default;
            return false;
        }
        value = node._value;
        return true;
    }

    public TValue GetOrDefault(TKey key) {
        var node = GetNode(key);
        return node == null ? default : node._value;
    }

    public TValue GetOrDefault(TKey key, TValue defVal) {
        var node = GetNode(key);
        return node == null ? defVal : node._value;
    }

    public bool GetAndMoveToFirst(TKey key, out TValue value) {
        var node = GetNode(key);
        if (node == null) {
            value = default;
            return false;
        }
        MoveToFirst(node);
        value = node._value;
        return true;
    }

    public bool GetAndMoveToLast(TKey key, out TValue value) {
        var node = GetNode(key);
        if (node == null) {
            value = default;
            return false;
        }
        MoveToLast(node);
        value = node._value;
        return true;
    }

    #endregion

    #region add

    public void Add(KeyValuePair<TKey, TValue> item) {
        bool modified = TryInsert(item.Key, item.Value, InsertionOrder.Default, InsertionBehavior.ThrowOnExisting);
        Debug.Assert(modified);
    }

    /// <summary>
    /// 如果key已经存在，则抛出异常
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <exception cref="ArgumentException">如果key已经存在</exception>
    public void Add(TKey key, TValue value) {
        bool modified = TryInsert(key, value, InsertionOrder.Default, InsertionBehavior.ThrowOnExisting);
        Debug.Assert(modified);
    }

    /// <summary>
    /// 如果key不存在则添加成功并返回true，否则返回false
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns>是否添加成功</returns>
    public bool TryAdd(TKey key, TValue value) {
        return TryInsert(key, value, InsertionOrder.Default, InsertionBehavior.None);
    }

    public void AddFirst(TKey key, TValue value) {
        bool modified = TryInsert(key, value, InsertionOrder.Head, InsertionBehavior.ThrowOnExisting);
        Debug.Assert(modified);
    }

    public bool TryAddFirst(TKey key, TValue value) {
        return TryInsert(key, value, InsertionOrder.Head, InsertionBehavior.None);
    }

    public void AddLast(TKey key, TValue value) {
        bool modified = TryInsert(key, value, InsertionOrder.Tail, InsertionBehavior.ThrowOnExisting);
        Debug.Assert(modified);
    }

    public bool TryAddLast(TKey key, TValue value) {
        return TryInsert(key, value, InsertionOrder.Tail, InsertionBehavior.None);
    }

    public PutResult<TValue> Put(TKey key, TValue value) {
        return TryPut(key, value, PutBehavior.None);
    }

    public PutResult<TValue> PutFirst(TKey key, TValue value) {
        return TryPut(key, value, PutBehavior.MoveToFirst);
    }

    public PutResult<TValue> PutLast(TKey key, TValue value) {
        return TryPut(key, value, PutBehavior.MoveToLast);
    }

    #endregion

    #region remove

    public bool Remove(KeyValuePair<TKey, TValue> item) {
        var node = GetNode(item.Key);
        if (node != null && ValComparer.Equals(node._value, item.Value)) {
            RemoveNode(node);
            return true;
        }
        return false;
    }

    public bool Remove(TKey key) {
        var node = GetNode(key);
        if (node == null) {
            return false;
        }
        RemoveNode(node);
        return true;
    }

    public bool Remove(TKey key, out TValue value) {
        var node = GetNode(key);
        if (node == null) {
            value = default;
            return false;
        }
        value = node._value;
        RemoveNode(node);
        return true;
    }

    public KeyValuePair<TKey, TValue> RemoveFirst() {
        if (TryRemoveFirst(out KeyValuePair<TKey, TValue> r)) {
            return r;
        }
        throw DictionaryEmptyException();
    }

    public bool TryRemoveFirst(out KeyValuePair<TKey, TValue> pair) {
        Node oldHead = _head;
        if (oldHead == null) {
            pair = default;
            return false;
        }

        pair = oldHead.AsPair();
        _count--;
        _version++;
        FixPointers(oldHead);
        ShiftKeys(oldHead._index);
        return true; // todo 尝试减少数组大小
    }

    public KeyValuePair<TKey, TValue> RemoveLast() {
        if (TryRemoveLast(out KeyValuePair<TKey, TValue> r)) {
            return r;
        }
        throw DictionaryEmptyException();
    }

    public bool TryRemoveLast(out KeyValuePair<TKey, TValue> pair) {
        Node oldTail = _tail;
        if (oldTail == null) {
            pair = default;
            return false;
        }
        pair = oldTail.AsPair();

        _count--;
        _version++;
        FixPointers(oldTail);
        ShiftKeys(oldTail._index);
        return true; // todo 尝试减少数组大小
    }

    public void Clear() {
        _version++; // 以防子类无权更新
        int count = _count;
        if (count > 0 && _table != null) {
            _count = 0;
            _head = _tail = null;
            Array.Clear(_table);
        }
    }

    #endregion

    #region copyto

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Array is too small");

        // 按照插入序返回
        for (Node e = _head; e != null; e = e._next) {
            array[arrayIndex++] = new KeyValuePair<TKey, TValue>(e._key, e._value);
        }
    }

    public void CopyTo(Array array, int arrayIndex) {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Array is too small");
        if (array.Rank != 1) throw new ArgumentException("RankMultiDimNotSupported");

        if (array is KeyValuePair<TKey, TValue>[] pairs) {
            CopyTo(pairs, arrayIndex);
        }
        else if (array is DictionaryEntry[] dictEntryArray) {
            for (Node e = _head; e != null; e = e._next) {
                dictEntryArray[arrayIndex++] = new DictionaryEntry(e._key, e._value);
            }
        }
        else {
            object[]? objects = array as object[];
            if (objects == null) throw new ArgumentException("InvalidArrayType");
            for (Node e = _head; e != null; e = e._next) {
                objects[arrayIndex++] = new KeyValuePair<TKey, TValue>(e._key, e._value);
            }
        }
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
            table = _table = new Node[_mask + 1];
        }
        int mask = _mask;
        IEqualityComparer<TKey> keyComparer = _keyComparer;
        // 先测试无冲突位置
        int pos = mask & hash;
        Node node = table[pos];
        if (node == null) return -(pos + 1);
        if (node._hash == hash && keyComparer.Equals(node._key, key)) {
            return pos;
        }
        // 线性探测
        // 注意：为了利用空间，线性探测需要在越界时绕回到数组首部(mask取余绕回)
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

    private Node? GetNode(TKey key) {
        Node[] table = _table;
        if (table == null || _count == 0) {
            return null;
        }
        int mask = _mask;
        IEqualityComparer<TKey> keyComparer = _keyComparer;
        int hash = HashCommon.Mix(keyComparer.GetHashCode(key));
        // 先测试无冲突位置
        int pos = mask & hash;
        Node node = table[pos];
        if (node == null) return null;
        if (node._hash == hash && keyComparer.Equals(node._key, key)) {
            return null;
        }
        for (int i = 0; i < mask; i++) {
            pos = (pos + 1) & mask;
            node = table[pos];
            if (node == null) return null;
            if (node._hash == hash && keyComparer.Equals(node._key, key)) {
                return null;
            }
        }
        throw new InvalidOperationException("state error");
    }

    private bool TryInsert(TKey key, TValue value, InsertionOrder order, InsertionBehavior behavior) {
        if (key == null) throw new ArgumentNullException(nameof(key));

        int hash = HashCommon.Mix(_keyComparer.GetHashCode(key));
        int pos = Find(key, hash);
        if (pos >= 0) {
            Node existNode = _table![pos]!;
            switch (behavior) {
                case InsertionBehavior.OverwriteExisting: {
                    existNode._value = value;
                    return true;
                }
                case InsertionBehavior.ThrowOnExisting: {
                    throw new InvalidOperationException("AddingDuplicateWithKey: " + key);
                }
                case InsertionBehavior.None:
                default: return false;
            }
        }

        pos = -pos - 1;
        Insert(pos, hash, key, value, order);
        return true;
    }

    private PutResult<TValue> TryPut(TKey key, TValue value, PutBehavior behavior) {
        if (key == null) throw new ArgumentNullException(nameof(key));

        int hash = HashCommon.Mix(_keyComparer.GetHashCode(key));
        int pos = Find(key, hash);
        if (pos >= 0) {
            Node existNode = _table![pos]!;
            PutResult<TValue> result = new PutResult<TValue>(true, existNode._value);
            existNode._value = value;
            if (behavior == PutBehavior.MoveToLast) {
                MoveToLast(existNode);
            }
            else if (behavior == PutBehavior.MoveToFirst) {
                MoveToFirst(existNode);
            }
            return result;
        }

        pos = -pos - 1;
        switch (behavior) {
            case PutBehavior.MoveToFirst:
                Insert(pos, hash, key, value, InsertionOrder.Head);
                break;
            case PutBehavior.MoveToLast:
                Insert(pos, hash, key, value, InsertionOrder.Tail);
                break;
            case PutBehavior.None:
            default:
                Insert(pos, hash, key, value, InsertionOrder.Default);
                break;
        }
        return PutResult<TValue>.Empty;
    }

    private void Insert(int pos, int hash, TKey key, TValue value, InsertionOrder order) {
        Node node = new Node(hash, key, value, pos);
        if (order == InsertionOrder.Head) {
            if (_count == 0) {
                _head = _tail = node;
            }
            else {
                node._next = _head;
                _head!._prev = node;
                _head = node;
            }
        }
        else {
            if (_count == 0) {
                _head = _tail = node;
            }
            else {
                node._prev = _tail;
                _tail!._next = node;
                _tail = node;
            }
        }
        _count++;
        _version++;
        if (_count >= _maxFill) {
            // Rehash(HashCommon.ArraySize(_count + 1, _loadFactor));
        }
    }

    private void Rehash(int newSize) {
        Debug.Assert(newSize >= _count);
        Node[] oldTable = _table!;

        Node[] newTable = new Node[newSize];
        int mask = newSize - 1;
        // Node上有缓存下标，因此想要做到完全的原子性是比较麻烦的，因此我们不考虑更新过程中奇怪的失败情况
        // 按照原来的table迭代，旧table中hash冲突的元素是连续的
        int tempHeadIndex = _head!._index;
        int newPos;
        for (int j = _count; j > 0; j--) {
            Node node = oldTable[tempHeadIndex];
            if (node == null) {
            }
        }

        this._table = newTable;
        this._mask = mask;
        this._maxFill = HashCommon.MaxFill(newSize, _loadFactor);
    }

    /** 删除指定节点 -- 该方法为通用情况；需要处理Head和Tail的情况 */
    private void RemoveNode(Node node) {
        _count--;
        _version++;
        FixPointers(node);
        ShiftKeys(node._index);
    }

    /// <summary>
    /// 删除pos位置的元素，将后续相同hash值的元素前移；
    /// 在调用该方法前，应当先调用 FixPointers 修正被删除节点的索引信息。
    /// </summary>
    /// <param name="pos"></param>
    private void ShiftKeys(int pos) {
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
    /// 在调用该方法前需要先更新count和version
    /// </summary>
    /// <param name="node">要解除引用的节点</param>
    /// <param name="removed">是否是删除元素</param>
    private void FixPointers(Node node, bool removed = true) {
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
        if (removed) {
            node.AfterRemoved();
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

    #region itr

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
        return new DictionaryIterator(this, false);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetReversedEnumerator() {
        return new DictionaryIterator(this, true);
    }

    private class DictionaryIterator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly LinkedDictionary<TKey, TValue> _dictionary;
        private readonly bool _reversed;

        private int _version;
        private KeyValuePair<TKey, TValue> _current;
        private Node? _node;

        public DictionaryIterator(LinkedDictionary<TKey, TValue> dictionary, bool reversed) {
            _dictionary = dictionary;
            _reversed = reversed;
            _version = dictionary._version;

            _node = reversed ? dictionary._tail : dictionary._head;
            _current = default;
        }

        public bool MoveNext() {
            if (_version != _dictionary._version) {
                throw new InvalidOperationException("EnumFailedVersion");
            }
            if (_node == null) {
                return false;
            }
            _node = _reversed ? _node._prev : _node._next;
            if (_node == null) {
                _current = default;
                return false;
            }
            _current = new KeyValuePair<TKey, TValue>(_node._key, _node._value);
            return true;
        }

        // 其实这期间node的value可能变化，安全的话应该每次创建新的Pair，但c#系统库没这么干
        public KeyValuePair<TKey, TValue> Current => _current;

        public void Reset() {
            if (_version != _dictionary._version) {
                throw new InvalidOperationException("EnumFailedVersion");
            }
            _node = _reversed ? _dictionary._tail : _dictionary._head;
            _current = default;
        }

        object IEnumerator.Current => Current;

        public void Dispose() {
            throw new NotImplementedException();
        }
    }

    #endregion


    #region keys/value

    private class KeyCollection : IGenericCollection<TKey>, IReadOnlyCollection<TKey>
    {
        private readonly LinkedDictionary<TKey, TValue> _dictionary;

        public KeyCollection(LinkedDictionary<TKey, TValue> dictionary) {
            _dictionary = dictionary;
        }

        public IEnumerator<TKey> GetEnumerator() {
            throw new NotImplementedException();
        }

        public void Add(TKey item) {
            throw new NotImplementedException();
        }

        public void Clear() {
            throw new NotImplementedException();
        }

        public bool Contains(TKey item) {
            throw new NotImplementedException();
        }

        public void CopyTo(TKey[] array, int arrayIndex) {
            throw new NotImplementedException();
        }

        public bool Remove(TKey item) {
            throw new NotImplementedException();
        }

        public int Count { get; }
        public bool IsReadOnly { get; }

        public void CopyTo(Array array, int index) {
            throw new NotImplementedException();
        }

        public bool IsSynchronized { get; }
        public object SyncRoot { get; }
    }

    private class ValueCollection : IGenericCollection<TValue>, IReadOnlyCollection<TValue>
    {
        private readonly LinkedDictionary<TKey, TValue> _dictionary;

        public ValueCollection(LinkedDictionary<TKey, TValue> dictionary) {
            _dictionary = dictionary;
        }

        public IEnumerator<TValue> GetEnumerator() {
            throw new NotImplementedException();
        }

        public void Add(TValue item) {
            throw new NotImplementedException();
        }

        public void Clear() {
            throw new NotImplementedException();
        }

        public bool Contains(TValue item) {
            throw new NotImplementedException();
        }

        public void CopyTo(TValue[] array, int arrayIndex) {
            throw new NotImplementedException();
        }

        public bool Remove(TValue item) {
            throw new NotImplementedException();
        }

        public int Count { get; }
        public bool IsReadOnly { get; }

        public void CopyTo(Array array, int index) {
            throw new NotImplementedException();
        }

        public bool IsSynchronized { get; }
        public object SyncRoot { get; }
    }

    #endregion

    #region Node

    private class Node : IEquatable<Node>
    {
        /** 由于Key的hash使用频率极高，缓存以减少求值开销 */
        internal readonly int _hash;
        internal readonly TKey _key;
        internal TValue? _value;
        /** 由于使用线性探测法，删除的元素不一定直接位于hash槽上，需要记录，以便快速删除；-1表示已删除 */
        internal int _index;

        internal Node? _prev;
        internal Node? _next;

        public Node(int hash, TKey key, TValue value, int index) {
            _hash = hash;
            _key = key;
            _value = value;
            _index = index;
        }

        public void AfterRemoved() {
            if (!ValueIsValueType) {
                _value = default;
            }
            _index = -1;
            _prev = null;
            _next = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyValuePair<TKey, TValue> AsPair() {
            return new KeyValuePair<TKey, TValue>(_key, _value);
        }

        public override int GetHashCode() {
            return _hash; // 不使用value计算hash，因为value可能在中途变更
        }

        #region equals

        public bool Equals(Node? other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TKey>.Default.Equals(_key, other._key) && EqualityComparer<TValue>.Default.Equals(_value, other._value);
        }

        public override bool Equals(object? obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Node)obj);
        }

        public static bool operator ==(Node? left, Node? right) {
            return Equals(left, right);
        }

        public static bool operator !=(Node? left, Node? right) {
            return !Equals(left, right);
        }

        public override string ToString() {
            return $"{nameof(_key)}: {_key}, {nameof(_value)}: {_value}";
        }

        #endregion
    }

    #endregion
}