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
using System.Runtime.Serialization;

namespace Wjybxx.Commons.Collections;

/// <summary>
/// 保持插入序的字典
/// 1.使用简单的线性探测法解决Hash冲突，因此在数据量较大的情况下查询性能可能会降低 -- 实际表现很好。
/// 2.算法参考自FastUtil的LinkedOpenHashMap。
/// 3.非线程安全。
///
/// 测试数据(在GetNode方法中记录线性探测次数)：
/// 1. 1W个int类型key，hash冲突后线性探测的平均值小于1 (总次数4000~5000)
/// 2. 10W个int类型key，hash冲突后线性探测的平均值小于1 (总次数11000~12000)
/// 3. 1W个string类型key，长度24，hash冲突后线性探测的平均值小于1 (总次数4000~5000)(与int相似，且调整长度几无变化)
/// 4. 10W个string类型key，长度24，hash冲突后线性探测的平均值小于1 (总次数11000~12000)(与int相似，且调整长度几无变化)
/// 
/// 吐槽：
/// 1.C#的基础库里居然没有保持插入序的高性能字典，这对于编写底层工具的开发者来说太不方便了。
/// 2.C#的集合和字典库接口太差了，泛型集合与非泛型集合兼容性也不够。
/// 
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
[Serializable]
public class LinkedDictionary<TKey, TValue> : ISequencedDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>,
    ISerializable where TKey : notnull
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

    /** 初始掩码 -- 可求得初始容量 */
    private int _initMask;
    /** 负载因子 */
    private float _loadFactor;
    /** 当前计算下标使用的掩码，不依赖数组长度，避免未来调整时破坏太大；相反，我们可以通过mask获得数组的真实长度 */
    private int _mask;
    /** count触发扩容的边界值 -- 缓存减少计算 */
    private int _maxFill;

    /** 用于代替key自身的equals和hashcode计算；这一点C#的设计做的要好些 */
    private IEqualityComparer<TKey> _keyComparer;
    private KeyCollection? _keys;
    private ValueCollection? _values;

    public LinkedDictionary()
        : this(0, HashCommon.DefaultLoadFactor) {
    }

    public LinkedDictionary(IDictionary<TKey, TValue> src)
        : this(src.Count, HashCommon.DefaultLoadFactor) {
        PutRange(src);
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
        _initMask = _mask = HashCommon.ArraySize(expectedCount, loadFactor) - 1;
        _maxFill = HashCommon.MaxFill(_mask + 1, loadFactor);
    }

    public int Count => _count;
    public bool IsReadOnly => false;

    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    bool IDictionary.IsFixedSize => false;
    bool IDictionary.IsReadOnly => false;

    public IGenericCollection<TKey> Keys => CachedKeys();
    public IGenericCollection<TValue> Values => CachedValues();
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => CachedKeys();
    ICollection<TValue> IDictionary<TKey, TValue>.Values => CachedValues();
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => CachedKeys();
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => CachedValues();

    private KeyCollection CachedKeys() {
        if (_keys == null) {
            _keys = new KeyCollection(this);
        }
        return _keys;
    }

    private ValueCollection CachedValues() {
        if (_values == null) {
            _values = new ValueCollection(this);
        }
        return _values;
    }

    public TValue this[TKey key] {
        get {
            Node? node = GetNode(key);
            if (node == null) {
                throw new KeyNotFoundException(key.ToString());
            }
            return node._value;
        }
        set => TryInsert(key, value, InsertionOrder.Default, InsertionBehavior.OverwriteExisting);
    }

    private IEqualityComparer<TValue> ValComparer => EqualityComparer<TValue>.Default;

    #region peek

    public bool PeekFirstPair(out KeyValuePair<TKey, TValue> pair) {
        if (_head != null) {
            pair = _head.AsPair();
            return true;
        }
        pair = default;
        return false;
    }

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

    public bool PeekLastPair(out KeyValuePair<TKey, TValue> pair) {
        if (_tail != null) {
            pair = _tail.AsPair();
            return true;
        }
        pair = default;
        return false;
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

    public TValue GetAndMoveToFirst(TKey key) {
        var node = GetNode(key);
        if (node == null) {
            throw new KeyNotFoundException(key.ToString());
        }
        MoveToFirst(node);
        return node._value;
    }

    public bool TryGetAndMoveToFirst(TKey key, out TValue value) {
        var node = GetNode(key);
        if (node == null) {
            value = default;
            return false;
        }
        MoveToFirst(node);
        value = node._value;
        return true;
    }

    public TValue GetAndMoveToLast(TKey key) {
        var node = GetNode(key);
        if (node == null) {
            throw new KeyNotFoundException(key.ToString());
        }
        MoveToLast(node);
        return node._value;
    }

    public bool TryGetAndMoveToLast(TKey key, out TValue value) {
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

    public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> collection) {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        if (collection is ICollection<KeyValuePair<TKey, TValue>> c) {
            if (_loadFactor <= 0.5f) {
                EnsureCapacity(c.Count); // 负载小于0.5，数组的长度将大于等于count的2倍，就能放下所有元素
            }
            else {
                TryCapacity(_count + c.Count);
            }
        }
        foreach (KeyValuePair<TKey, TValue> pair in collection) {
            Add(pair.Key, pair.Value);
        }
    }

    public void PutRange(IEnumerable<KeyValuePair<TKey, TValue>> collection) {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        if (collection is ICollection<KeyValuePair<TKey, TValue>> c) {
            if (_loadFactor <= 0.5f) {
                EnsureCapacity(c.Count); // 负载小于0.5，数组的长度将大于等于count的2倍，就能放下所有元素
            }
            else {
                TryCapacity(_count + c.Count);
            }
        }
        foreach (KeyValuePair<TKey, TValue> pair in collection) {
            TryPut(pair.Key, pair.Value, PutBehavior.None);
        }
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
        oldHead.AfterRemoved();
        return true;
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
        oldTail.AfterRemoved();
        return true;
    }

    public void Clear() {
        int count = _count;
        if (count > 0 && _table != null) {
            _count = 0;
            _version++;
            _head = _tail = null;
            Array.Clear(_table);
        }
    }

    /** 用于子类更新版本号 */
    protected void IncVersion() => _version++;

    #endregion

    #region sp

    /// <summary>
    /// 查询指定键的后一个键
    /// </summary>
    /// <param name="key">当前键</param>
    /// <param name="next">接收下一个键</param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">如果当前键不存在</exception>
    public bool NextKey(TKey key, out TKey next) {
        var node = GetNode(key);
        if (node == null) {
            throw new KeyNotFoundException(key.ToString());
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
            throw new KeyNotFoundException(key.ToString());
        }
        if (node._prev != null) {
            prev = node._prev._key;
            return true;
        }
        prev = default;
        return false;
    }

    public void AdjustCapacity(int expectedCount, bool ignoreInitCount = false) {
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
            if (arraySize <= (_initMask + 1) && !ignoreInitCount) {
                return; // 不能小于初始容量
            }
            if (Math.Abs(arraySize - curArraySize) <= HashCommon.DefaultInitialSize) {
                return; // 避免不必要的收缩
            }
        }
        Rehash(arraySize);
    }

    #endregion

    #region copyto

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
        CopyTo(array, arrayIndex, false);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex, bool reversed) {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Array is too small");

        if (reversed) {
            for (Node e = _tail; e != null; e = e._prev) {
                array[arrayIndex++] = new KeyValuePair<TKey, TValue>(e._key, e._value);
            }
        }
        else {
            for (Node e = _head; e != null; e = e._next) {
                array[arrayIndex++] = new KeyValuePair<TKey, TValue>(e._key, e._value);
            }
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

    public void CopyKeysTo(TKey[] array, int arrayIndex, bool reversed) {
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

    private void CopyKeysTo(Array array, int arrayIndex) {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Array is too small");
        if (array.Rank != 1) throw new ArgumentException("RankMultiDimNotSupported");

        if (array is TKey[] castArray) {
            CopyKeysTo(castArray, arrayIndex, false);
        }
        else {
            object[]? objects = array as object[];
            if (objects == null) throw new ArgumentException("InvalidArrayType");
            for (Node e = _head; e != null; e = e._next) {
                objects[arrayIndex++] = e._key;
            }
        }
    }

    public void CopyValuesTo(TValue[] array, int arrayIndex, bool reversed) {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Array is too small");

        if (reversed) {
            for (Node e = _tail; e != null; e = e._prev) {
                array[arrayIndex++] = e._value;
            }
        }
        else {
            for (Node e = _head; e != null; e = e._next) {
                array[arrayIndex++] = e._value;
            }
        }
    }

    private void CopyValuesTo(Array array, int arrayIndex) {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Array is too small");
        if (array.Rank != 1) throw new ArgumentException("RankMultiDimNotSupported");

        if (array is TValue[] castArray) {
            CopyValuesTo(castArray, arrayIndex);
        }
        else {
            object[]? objects = array as object[];
            if (objects == null) throw new ArgumentException("InvalidArrayType");
            for (Node e = _head; e != null; e = e._next) {
                objects[arrayIndex++] = e._value;
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

    /** 该接口仅适用于查询方法使用 */
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
        if (_count >= _maxFill) {
            Rehash(HashCommon.ArraySize(_count + 1, _loadFactor));
        }
    }

    private void Rehash(int newSize) {
        Debug.Assert(newSize >= _count);
        Node[] oldTable = _table!;
        Node[] newTable = new Node[newSize];

        int mask = newSize - 1;
        int pos;
        // 遍历旧table数组会更快，数据更连续
        int remain = _count;
        for (var i = 0; i < oldTable.Length; i++) {
            var node = oldTable[i];
            if (node == null) {
                continue;
            }
            pos = node._hash & mask;
            while (newTable[pos] != null) {
                pos = (pos + 1) & mask;
            }
            newTable[pos] = node;
            node._index = pos;
            if (--remain == 0) {
                break;
            }
        }
        this._table = newTable;
        this._mask = mask;
        this._maxFill = HashCommon.MaxFill(newSize, _loadFactor);
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

    #region keys/values

    private class KeyCollection : IGenericCollection<TKey>
    {
        private readonly LinkedDictionary<TKey, TValue> _dictionary;

        public KeyCollection(LinkedDictionary<TKey, TValue> dictionary) {
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;
        public bool IsReadOnly => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public bool Contains(TKey item) {
            return item != null && _dictionary.ContainsKey(item);
        }

        public void CopyTo(TKey[] array, int arrayIndex) {
            _dictionary.CopyKeysTo(array, arrayIndex, false);
        }

        public void CopyTo(Array array, int index) {
            _dictionary.CopyKeysTo(array, index);
        }

        public IEnumerator<TKey> GetEnumerator() {
            return new KeyIterator(_dictionary, false);
        }

        public void Add(TKey item) {
            throw new InvalidOperationException("NotSupported_KeyCollectionSet");
        }

        public void Clear() {
            throw new InvalidOperationException("NotSupported_KeyCollectionSet");
        }

        public bool Remove(TKey item) {
            throw new InvalidOperationException("NotSupported_KeyCollectionSet");
        }
    }

    private class ValueCollection : IGenericCollection<TValue>
    {
        private readonly LinkedDictionary<TKey, TValue> _dictionary;

        public ValueCollection(LinkedDictionary<TKey, TValue> dictionary) {
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;
        public bool IsReadOnly => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public bool Contains(TValue item) {
            return _dictionary.ContainsValue(item);
        }

        public void CopyTo(TValue[] array, int arrayIndex) {
            _dictionary.CopyValuesTo(array, arrayIndex, false);
        }

        public void CopyTo(Array array, int index) {
            _dictionary.CopyValuesTo(array, index);
        }

        public IEnumerator<TValue> GetEnumerator() {
            return new ValueIterator(_dictionary, false);
        }

        public void Add(TValue item) {
            throw new InvalidOperationException("NotSupported_ValueCollectionSet");
        }

        public void Clear() {
            throw new InvalidOperationException("NotSupported_ValueCollectionSet");
        }

        public bool Remove(TValue item) {
            throw new InvalidOperationException("NotSupported_ValueCollectionSet");
        }
    }

    #endregion

    #region itr

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
        return new PairIterator(this, false);
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetReversedEnumerator() {
        return new PairIterator(this, true);
    }

    private static readonly Node UnsetNode = new Node(0, default, default, -1);
    private static readonly Node DisposedNode = new Node(0, default, default, -1);

    private abstract class AbstractIterator<T> : IEnumerator<T>
    {
        private readonly LinkedDictionary<TKey, TValue> _dictionary;
        private readonly bool _reversed;

        private int _version;
        private Node? _node;
        private T _current;

        protected AbstractIterator(LinkedDictionary<TKey, TValue> dictionary, bool reversed) {
            _dictionary = dictionary;
            _reversed = reversed;
            _version = dictionary._version;

            _node = UnsetNode;
            _current = default;
        }

        public bool MoveNext() {
            if (_version != _dictionary._version) {
                throw new InvalidOperationException("EnumFailedVersion");
            }
            if (_node == null) {
                return false;
            }
            if (ReferenceEquals(_node, UnsetNode)) {
                _node = _reversed ? _dictionary._tail : _dictionary._head;
            }
            else {
                _node = _reversed ? _node._prev : _node._next;
            }
            if (_node == null) {
                _current = default;
                return false;
            }
            // 其实这期间node的value可能变化，安全的话应该每次创建新的Pair，但c#系统库没这么干
            _current = CurrentOfNode(_node);
            return true;
        }

        protected abstract T CurrentOfNode(Node node);

        public void Reset() {
            if (_version != _dictionary._version) {
                throw new InvalidOperationException("EnumFailedVersion");
            }
            _node = _reversed ? _dictionary._tail : _dictionary._head;
            _current = default;
        }

        public T Current => _current;

        object IEnumerator.Current => Current;

        public void Dispose() {
        }
    }

    private class PairIterator : AbstractIterator<KeyValuePair<TKey, TValue>>
    {
        public PairIterator(LinkedDictionary<TKey, TValue> dictionary, bool reversed) : base(dictionary, reversed) {
        }

        protected override KeyValuePair<TKey, TValue> CurrentOfNode(Node node) {
            return new KeyValuePair<TKey, TValue>(node._key, node._value);
        }
    }

    private class KeyIterator : AbstractIterator<TKey>
    {
        public KeyIterator(LinkedDictionary<TKey, TValue> dictionary, bool reversed) : base(dictionary, reversed) {
        }

        protected override TKey CurrentOfNode(Node node) {
            return node._key;
        }
    }

    private class ValueIterator : AbstractIterator<TValue>
    {
        public ValueIterator(LinkedDictionary<TKey, TValue> dictionary, bool reversed) : base(dictionary, reversed) {
        }

        protected override TValue CurrentOfNode(Node node) {
            return node._value;
        }
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

    #region seril

    private const string Names_InitMask = "InitMask";
    private const string Names_LoadFactor = "LoadFactor";
    private const string Names_Comparer = "Comparer";
    private const string Names_Pairs = "KeyValuePairs";

    public virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
        if (info == null) throw new ArgumentNullException(nameof(info));

        info.AddValue(Names_InitMask, _initMask);
        info.AddValue(Names_LoadFactor, _loadFactor);
        info.AddValue(Names_Comparer, _keyComparer, typeof(IEqualityComparer<TKey>));
        if (_table != null && _count > 0) { // 有数据才序列化
            var array = new KeyValuePair<TKey, TValue>[Count];
            CopyTo(array, 0, false);
            info.AddValue(Names_Pairs, array, typeof(KeyValuePair<TKey, TValue>[]));
        }
    }

    protected LinkedDictionary(SerializationInfo info, StreamingContext context) {
        this._initMask = info.GetInt32(Names_InitMask);
        this._loadFactor = info.GetSingle(Names_LoadFactor);
        this._keyComparer = (IEqualityComparer<TKey>)info.GetValue(Names_Comparer, typeof(IEqualityComparer<TKey>)) ?? EqualityComparer<TKey>.Default;
        if (_initMask < HashCommon.MinArraySize - 1) {
            throw new SerializationException("invalid serial data");
        }

        KeyValuePair<TKey, TValue>[] keyValuePairs = (KeyValuePair<TKey, TValue>[])info.GetValue(Names_Pairs, typeof(KeyValuePair<TKey, TValue>[]));
        if (keyValuePairs != null && keyValuePairs.Length > 0) {
            _mask = HashCommon.ArraySize(keyValuePairs.Length, _loadFactor) - 1;
            _maxFill = HashCommon.MaxFill(_mask + 1, _loadFactor);
            BuildTable(keyValuePairs);
        }
        else {
            _mask = _initMask;
            _maxFill = HashCommon.MaxFill(_mask + 1, _loadFactor);
        }
    }

    private void BuildTable(KeyValuePair<TKey, TValue>[] keyValuePairs) {
        // 构建Node链
        IEqualityComparer<TKey> keyComparer = _keyComparer;
        Node head;
        {
            KeyValuePair<TKey, TValue> pair = keyValuePairs[0];
            int hash = HashCommon.Mix(keyComparer.GetHashCode(pair.Key));
            head = new Node(hash, pair.Key, pair.Value, -1);
        }
        Node tail = head;
        for (var i = 1; i < keyValuePairs.Length; i++) {
            KeyValuePair<TKey, TValue> pair = keyValuePairs[i];
            int hash = HashCommon.Mix(keyComparer.GetHashCode(pair.Key));
            Node next = new Node(hash, pair.Key, pair.Value, -1);
            //
            tail._next = next;
            next._prev = tail;
            tail = next;
        }
        _head = head;
        _tail = tail;

        // 散列到数组 -- 不走rehash避免创建辅助空间
        Node[] newTable = new Node[_mask + 1];
        int mask = _mask;
        int pos;
        for (Node node = _head; node != null; node = node._next) {
            pos = node._hash & mask;
            while (newTable[pos] != null) {
                pos = (pos + 1) & mask;
            }
            newTable[pos] = node;
            node._index = pos;
        }
        _table = newTable;
        _count = keyValuePairs.Length;
    }

    #endregion
}